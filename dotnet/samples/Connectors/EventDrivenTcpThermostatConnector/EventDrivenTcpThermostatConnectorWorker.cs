// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using System.Net.Sockets;

namespace EventDrivenTcpThermostatConnector
{
    public class EventDrivenTcpThermostatConnectorWorker : BackgroundService, IDisposable
    {
        private readonly ILogger<EventDrivenTcpThermostatConnectorWorker> _logger;
        private readonly ConnectorWorker _connector;

        private const string InboundEndpointName = "my_tcp_endpoint";

        public EventDrivenTcpThermostatConnectorWorker(ApplicationContext applicationContext, ILogger<EventDrivenTcpThermostatConnectorWorker> logger, ILogger<ConnectorWorker> connectorLogger, IMqttClient mqttClient, IMessageSchemaProvider datasetSamplerFactory, IAdrClientWrapperProvider adrClientFactory, IConnectorLeaderElectionConfigurationProvider leaderElectionConfigurationProvider)
        {
            _logger = logger;
            _connector = new(applicationContext, connectorLogger, mqttClient, datasetSamplerFactory, adrClientFactory, leaderElectionConfigurationProvider)
            {
                WhileAssetIsAvailable = WhileAssetAvailableAsync,
                WhileDeviceIsAvailable = WhileDeviceAvailableAsync,
            };
        }

        private async Task WhileDeviceAvailableAsync(DeviceAvailableEventArgs args, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Device with name {0} is now available", args.DeviceName);

            DeviceStatus deviceStatus = args.DeviceEndpointClient.BuildOkayStatus();
            try
            {
                _logger.LogInformation("Reporting device status as okay to Azure Device Registry service...");
                await args.DeviceEndpointClient.UpdateDeviceStatusAsync(deviceStatus, null, cancellationToken);
            }
            catch (Exception e2)
            {
                _logger.LogError(e2, "Failed to report device status to Azure Device Registry service");
            }
        }

        private async Task WhileAssetAvailableAsync(AssetAvailableEventArgs args, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Asset with name {0} is now sampleable", args.AssetName);
            cancellationToken.ThrowIfCancellationRequested();

            if (args.Asset.EventGroups == null || args.Asset.EventGroups.Count != 1)
            {
                _logger.LogWarning("Asset with name {0} does not have the expected event group. No events will be received.", args.AssetName);
                AssetStatus assetStatus = args.AssetClient.BuildOkayStatus();
                try
                {
                    await args.AssetClient.UpdateAssetStatusAsync(assetStatus, null, cancellationToken);
                }
                catch (Exception e2)
                {
                    _logger.LogError(e2, "Failed to report device status to Azure Device Registry service");
                }
                return;
            }

            var eventGroup = args.Asset.EventGroups.First();
            if (eventGroup.Events == null || eventGroup.Events.Count != 1)
            {
                _logger.LogWarning("Asset with name {0} does not have the expected event within its event group. No events will be received.", args.AssetName);
                AssetStatus assetStatus = args.AssetClient.BuildOkayStatus();
                try
                {
                    await args.AssetClient.UpdateAssetStatusAsync(assetStatus, null, cancellationToken);
                }
                catch (Exception e2)
                {
                    _logger.LogError(e2, "Failed to report device status to Azure Device Registry service");
                }
                return;
            }

            // This sample only has one asset with one event
            var assetEvent = eventGroup.Events[0];

            if (assetEvent.DataSource == null || !int.TryParse(assetEvent.DataSource, out int port))
            {
                // If the asset's has no event doesn't specify a port, then do nothing
                _logger.LogError("Asset with name {0} has an event, but the event didn't configure a port, so the connector won't handle these events", args.AssetName);
                AssetStatus assetStatus = args.AssetClient.BuildOkayStatus();
                assetStatus.EventGroups!.First().Events!.First().Error = new ConfigError()
                {
                    Message = "The configured event was either missing the expected port or had a non-integer value for the port",
                };

                try
                {
                    _logger.LogInformation("Reporting asset status as okay to Azure Device Registry service...");
                    await args.AssetClient.UpdateAssetStatusAsync(assetStatus, null, cancellationToken);
                }
                catch (Exception e2)
                {
                    _logger.LogError(e2, "Failed to report device status to Azure Device Registry service");
                }
                return;
            }

            await OpenTcpConnectionAsync(args, args.Asset.EventGroups.First().Name, assetEvent, port, cancellationToken);
        }

        private async Task OpenTcpConnectionAsync(AssetAvailableEventArgs args, string eventGroupName, AssetEvent assetEvent, int port, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    //tcp-service.azure-iot-operations.svc.cluster.local:80
                    if (args.Device.Endpoints == null
                        || args.Device.Endpoints.Inbound == null)
                    {
                        _logger.LogError("Missing TCP server address configuration");
                        return;
                    }

                    string host = args.Device.Endpoints.Inbound[InboundEndpointName].Address.Split(":")[0];
                    _logger.LogInformation("Attempting to open TCP client with address {0} and port {1}", host, port);
                    using TcpClient client = new();
                    await client.ConnectAsync(host, port, cancellationToken);
                    await using NetworkStream stream = client.GetStream();

                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            byte[] buffer = new byte[1024];
                            int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1024), cancellationToken);
                            Array.Resize(ref buffer, bytesRead);

                            _logger.LogInformation("Received data from event with name {0} on asset with name {1}. Forwarding this data to the MQTT broker.", assetEvent.Name, args.AssetName);
                            await args.AssetClient.ForwardReceivedEventAsync(eventGroupName, assetEvent, buffer, null, cancellationToken);

                            // Report status of the asset once the first event has been received and forwarded
                            AssetStatus assetStatus = args.AssetClient.BuildOkayStatus();
                            try
                            {
                                await args.AssetClient.UpdateAssetStatusAsync(assetStatus, null, cancellationToken);
                            }
                            catch (Exception e2)
                            {
                                _logger.LogError(e2, "Failed to report device status to Azure Device Registry service");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to listen on TCP connection");
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to open TCP connection to asset");
                }

                DeviceStatus deviceStatus = args.DeviceEndpointClient.BuildOkayStatus();
                deviceStatus.SetEndpointError(
                    InboundEndpointName,
                    new ConfigError()
                    {
                        Message = "Unable to connect to the TCP endpoint. The connector will retry to connect."
                    });

                try
                {
                    await args.DeviceEndpointClient.UpdateDeviceStatusAsync(deviceStatus, null, cancellationToken);
                }
                catch (Exception e2)
                {
                    _logger.LogError(e2, "Failed to report device status to Azure Device Registry service");
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting the connector...");
            await _connector.RunConnectorAsync(cancellationToken);
        }

        public override void Dispose()
        {
            base.Dispose();
            _connector.WhileAssetIsAvailable -= WhileAssetAvailableAsync;
            _connector.Dispose();
        }
    }
}
