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

        public EventDrivenTcpThermostatConnectorWorker(ApplicationContext applicationContext, ILogger<EventDrivenTcpThermostatConnectorWorker> logger, ILogger<ConnectorWorker> connectorLogger, IMqttClient mqttClient, IMessageSchemaProvider datasetSamplerFactory, IAdrClientWrapperProvider adrClientFactory, IConnectorLeaderElectionConfigurationProvider leaderElectionConfigurationProvider)
        {
            _logger = logger;
            _connector = new(applicationContext, connectorLogger, mqttClient, datasetSamplerFactory, adrClientFactory, leaderElectionConfigurationProvider)
            {
                WhileAssetIsAvailable = WhileAssetAvailableAsync
            };
        }

        private async Task WhileAssetAvailableAsync(AssetAvailableEventArgs args, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Asset with name {0} is now sampleable", args.AssetName);
            cancellationToken.ThrowIfCancellationRequested();

            if (args.Asset.Events == null)
            {
                // If the asset has no datasets to sample, then do nothing
                _logger.LogError("Asset with name {0} does not have the expected event", args.AssetName);
                return;
            }

            // This sample only has one asset with one event
            var assetEvent = args.Asset.Events[0];

            if (assetEvent.EventNotifier == null || !int.TryParse(assetEvent.EventNotifier, out int port))
            {
                // If the asset's has no event doesn't specify a port, then do nothing
                _logger.LogInformation("Asset with name {0} has an event, but the event didn't configure a port, so the connector won't handle these events", args.AssetName);
                return;
            }

            await OpenTcpConnectionAsync(args, assetEvent, port, cancellationToken);
        }

        private async Task OpenTcpConnectionAsync(AssetAvailableEventArgs args, AssetEvent assetEvent, int port, CancellationToken cancellationToken)
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

                    string host = args.Device.Endpoints.Inbound["my_tcp_endpoint"].Address.Split(":")[0];
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
                            await _connector.ForwardReceivedEventAsync(args.DeviceName, args.InboundEndpointName, args.Asset, args.AssetName, assetEvent, buffer, null, cancellationToken);
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
