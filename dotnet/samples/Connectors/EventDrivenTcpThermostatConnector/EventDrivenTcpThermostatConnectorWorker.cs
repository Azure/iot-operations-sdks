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
        private CancellationTokenSource? _tcpConnectionCancellationToken;

        public EventDrivenTcpThermostatConnectorWorker(ApplicationContext applicationContext, ILogger<EventDrivenTcpThermostatConnectorWorker> logger, ILogger<ConnectorWorker> connectorLogger, IMqttClient mqttClient, IMessageSchemaProvider datasetSamplerFactory, IAdrClientWrapper assetMonitor, IConnectorLeaderElectionConfigurationProvider leaderElectionConfigurationProvider)
        {
            _logger = logger;
            _connector = new(applicationContext, connectorLogger, mqttClient, datasetSamplerFactory, assetMonitor, leaderElectionConfigurationProvider);
            _connector.OnAssetAvailable += OnAssetAvailableAsync;
            _connector.OnAssetUnavailable += OnAssetUnavailableAsync;
        }

        private async void OnAssetAvailableAsync(object? sender, AssetAvailableEventArgs args)
        {
            _logger.LogInformation("Asset with name {0} is now sampleable", args.AssetName);

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

            await OpenTcpConnectionAsync(args, assetEvent, port);
        }

        private async Task OpenTcpConnectionAsync(AssetAvailableEventArgs args, AssetEventSchemaElement assetEvent, int port)
        {
            _tcpConnectionCancellationToken = new();
            try
            {
                //tcp-service.azure-iot-operations.svc.cluster.local:80
                if (args.Device.Endpoints == null
                    || args.Device.Endpoints.Inbound == null)
                {
                    _logger.LogError("Missing TCP server address configuration");
                    return;
                }

                string host = args.Device.Endpoints.Inbound["my-tcp-endpoint"].Address.Split(":")[0];
                _logger.LogInformation("Attempting to open TCP client with address {0} and port {1}", host, port);
                using TcpClient client = new();
                await client.ConnectAsync(host, port, _tcpConnectionCancellationToken.Token);
                await using NetworkStream stream = client.GetStream();

                try
                {
                    while (true)
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1024), _tcpConnectionCancellationToken.Token);
                        Array.Resize(ref buffer, bytesRead);

                        _logger.LogInformation("Received data from event with name {0} on asset with name {1}. Forwarding this data to the MQTT broker.", assetEvent.Name, args.AssetName);
                        await _connector.ForwardReceivedEventAsync(args.Asset, assetEvent, buffer, _tcpConnectionCancellationToken.Token);
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

        private void OnAssetUnavailableAsync(object? sender, AssetUnavailableEventArgs args)
        {
            _logger.LogInformation("Asset with name {0} is no longer sampleable", args.AssetName);
            _tcpConnectionCancellationToken?.Cancel();
            _tcpConnectionCancellationToken?.Dispose();
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting the connector...");
            await _connector.RunConnectorAsync(cancellationToken);
        }

        public override void Dispose()
        {
            base.Dispose();
            _tcpConnectionCancellationToken?.Dispose();
            _connector.OnAssetAvailable -= OnAssetAvailableAsync;
            _connector.OnAssetUnavailable -= OnAssetUnavailableAsync;
            _connector.Dispose();
        }
    }
}
