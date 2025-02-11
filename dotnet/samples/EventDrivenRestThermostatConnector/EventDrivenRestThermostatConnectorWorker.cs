// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.Assets;
using System.Net.Sockets;

namespace Azure.Iot.Operations.Connector
{
    public class EventDrivenRestThermostatConnectorWorker : BackgroundService, IDisposable
    {
        private readonly ILogger<EventDrivenRestThermostatConnectorWorker> _logger;
        private readonly TelemetryConnectorWorker _connector;
        private TcpListener? _tcpListener;

        public EventDrivenRestThermostatConnectorWorker(ILogger<EventDrivenRestThermostatConnectorWorker> logger, ILogger<TelemetryConnectorWorker> connectorLogger, IMqttClient mqttClient, IMessageSchemaProviderFactory datasetSamplerFactory, IAssetMonitor assetMonitor)
        {
            _logger = logger;
            _connector = new(connectorLogger, mqttClient, datasetSamplerFactory, assetMonitor);
            _connector.OnAssetAvailable += OnAssetAvailableAsync;
            _connector.OnAssetUnavailable += OnAssetUnavailableAsync;
        }

        private void OnAssetAvailableAsync(object? sender, AssetAvailabileEventArgs args)
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

            _tcpListener = new TcpListener(System.Net.IPAddress.Any, port);
            _logger.LogInformation("Starting TCP listener");
            _tcpListener.Start();

            // Spawn a task that listens for incoming data on the TCP port
            _ = new Task(async () =>
            {
                while (true)
                {
                    try
                    {
                        using TcpClient handler = await _tcpListener.AcceptTcpClientAsync();
                        await using NetworkStream stream = handler.GetStream();

                        byte[] buffer = new byte[1024];
                        int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1024));
                        Array.Resize(ref buffer, bytesRead);

                        _logger.LogInformation("Received data from event with name {0} on asset with name {1}. Forwarding this data to the MQTT broker.", assetEvent.Name, args.AssetName);
                        await _connector.ForwardReceivedEventAsync(args.AssetName, assetEvent.Name, buffer);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to listen on TCP connection");
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                }
            });
        }

        private void OnAssetUnavailableAsync(object? sender, AssetUnavailabileEventArgs args)
        {
            _logger.LogInformation("Asset with name {0} is no longer sampleable", args.AssetName);
            _tcpListener?.Stop();
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting the connector...");
            await _connector.RunConnectorAsync(cancellationToken);
        }

        public override void Dispose()
        {
            base.Dispose();
            _connector.OnAssetAvailable -= OnAssetAvailableAsync;
            _connector.OnAssetUnavailable -= OnAssetUnavailableAsync;
            _connector.Dispose();
            _tcpListener?.Dispose();
        }
    }
}