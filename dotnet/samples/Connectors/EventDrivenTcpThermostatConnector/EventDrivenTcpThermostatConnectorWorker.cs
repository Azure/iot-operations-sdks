// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using SimpleRpcServer;
using System.Net.Sockets;

namespace EventDrivenTcpThermostatConnector
{
    public class EventDrivenTcpThermostatConnectorWorker : BackgroundService, IDisposable
    {
        private readonly ILogger<EventDrivenTcpThermostatConnectorWorker> _logger;
        private readonly ConnectorWorker _connector;

        private const string InboundEndpointName = "my_tcp_endpoint";
        private readonly SimpleRpcServer.SampleCommandExecutor _rpcServer;

        public EventDrivenTcpThermostatConnectorWorker(ApplicationContext applicationContext, ILogger<EventDrivenTcpThermostatConnectorWorker> logger, ILogger<ConnectorWorker> connectorLogger, IMqttClient mqttClient, IMessageSchemaProvider datasetSamplerFactory, IAzureDeviceRegistryClientWrapperProvider adrClientFactory, IConnectorLeaderElectionConfigurationProvider leaderElectionConfigurationProvider)
        {
            _rpcServer = new(applicationContext, mqttClient, "someCommandName", new SimpleRpcServer.Utf8JsonSerializer())
            {
                OnCommandReceived = CommandHandler,
            };
            _logger = logger;
            _connector = new(applicationContext, connectorLogger, mqttClient, datasetSamplerFactory, adrClientFactory, leaderElectionConfigurationProvider)
            {
                WhileAssetIsAvailable = WhileAssetAvailableAsync,
                WhileDeviceIsAvailable = WhileDeviceAvailableAsync,
            };
        }

        private Task<ExtendedResponse<PayloadObject>> CommandHandler(ExtendedRequest<PayloadObject> request, CancellationToken token)
        {
            _logger.LogInformation("Handling an mRPC call");
            CommandResponseMetadata responseMetadata = new CommandResponseMetadata();
            var crm = new CommandRequestMetadata();
            long stageFourTicks = DateTime.UtcNow.Ticks;
            responseMetadata.UserData.Add("Stage4", stageFourTicks + "");
            return Task.FromResult(new ExtendedResponse<PayloadObject>()
            {
                Response = new PayloadObject(),
                ResponseMetadata = responseMetadata,
            });
        }

        private Task WhileDeviceAvailableAsync(DeviceAvailableEventArgs args, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task WhileAssetAvailableAsync(AssetAvailableEventArgs args, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip sampling if the device is explicitly disabled (Enabled is false). Undefined (null) value is treated as enabled.
            if (args.Device.Enabled != true && args.Device.Enabled != null)
            {
                throw new Exception();
            }

            // Skip sampling if the device is explicitly disabled (Enabled is false). Undefined (null) value is treated as enabled.
            if (args.Device.Enabled != true && args.Device.Enabled != null)
            {
                throw new Exception();
            }


            if (args.Asset.EventGroups == null || args.Asset.EventGroups.Count != 1)
            {
                throw new Exception();
            }

            var eventGroup = args.Asset.EventGroups.First();
            if (eventGroup.Events == null || eventGroup.Events.Count != 1)
            {
                throw new Exception();
            }

            // This sample only has one asset with one event
            var assetEvent = eventGroup.Events[0];

            if (assetEvent.DataSource == null || !int.TryParse(assetEvent.DataSource, out int port))
            {
                throw new Exception();
            }

            try
            {
                _logger.LogInformation("Starting mRPC server");

                await _rpcServer.StartAsync(null, cancellationToken);

                await OpenTcpConnectionAsync(args, args.Asset.EventGroups.First().Name, assetEvent, port, cancellationToken);
            }
            finally
            {
                await _rpcServer.StopAsync();
            }
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
                        return;
                    }

                    string host = args.Device.Endpoints.Inbound[InboundEndpointName].Address.Split(":")[0];
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

                            await args.AssetClient.ForwardReceivedEventAsync(eventGroupName, assetEvent, buffer, null, null, cancellationToken);
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
