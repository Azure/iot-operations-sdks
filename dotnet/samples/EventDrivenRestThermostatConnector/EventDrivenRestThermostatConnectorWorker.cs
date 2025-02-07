// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.Assets;
using EventDrivenRestThermostatConnector;
using System.Net.Sockets;

namespace Azure.Iot.Operations.Connector
{
    public class EventDrivenRestThermostatConnectorWorker : BackgroundService, IDisposable
    {
        private readonly ILogger<EventDrivenRestThermostatConnectorWorker> _logger;
        private readonly TelemetryConnectorWorker _connector;
        private TcpListener? _tcpListener;

        public EventDrivenRestThermostatConnectorWorker(ILogger<EventDrivenRestThermostatConnectorWorker> logger, ILogger<TelemetryConnectorWorker> connectorLogger, IMqttClient mqttClient, IDatasetMessageSchemaProviderFactory datasetSamplerFactory, IAssetMonitor assetMonitor)
        {
            _logger = logger;
            _connector = new(connectorLogger, mqttClient, datasetSamplerFactory, assetMonitor);
            _connector.OnAssetAvailable += OnAssetSampleableAsync;
            _connector.OnAssetUnavailable += OnAssetNotSampleableAsync;
        }

        private async void HandleReceivedData(object? sender, MockDataReceivedEventArgs e)
        {
            _logger.LogInformation("Received data from dataset with name {0} on asset with name {1}. Forwarding this data to the MQTT broker.", e.DatasetName, e.AssetName);
            await _connector.ForwardSampledDatasetAsync(e.AssetName, e.DatasetName, e.Data);
        }

        private void OnAssetSampleableAsync(object? sender, AssetAvailabileEventArgs args)
        {
            _logger.LogInformation("Asset with name {0} is now sampleable", args.AssetName);

            if (args.Asset.Datasets == null)
            {
                // If the asset has no datasets to sample, then do nothing
                return;
            }

            // This sample only has one asset with one dataset
            var dataset = args.Asset.Datasets[0];
            _tcpListener = new TcpListener(System.Net.IPAddress.Any, 80);
            _tcpListener.Start();

            // Spawn a task that listens for incoming data on the TCP port
            _ = new Task(async () =>
            {
                using TcpClient handler = await _tcpListener.AcceptTcpClientAsync();
                await using NetworkStream stream = handler.GetStream();

                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, 1024);
                Array.Resize(ref buffer, bytesRead);

                HandleReceivedData(this, new(buffer, args.AssetName, dataset.Name));
            });
        }

        private void OnAssetNotSampleableAsync(object? sender, AssetUnavailabileEventArgs args)
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
            _connector.OnAssetAvailable -= OnAssetSampleableAsync;
            _connector.OnAssetUnavailable -= OnAssetNotSampleableAsync;
            _connector.Dispose();
            _tcpListener?.Dispose();
        }
    }
}