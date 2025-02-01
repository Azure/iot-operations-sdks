// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public class EventDrivenWorker : BackgroundService
    {
        private EventDrivenTelemetryConnectorWorker _connector;
        private ILogger<EventDrivenTelemetryConnectorWorker> _logger;

        public EventDrivenWorker(ILogger<EventDrivenTelemetryConnectorWorker> logger, EventDrivenTelemetryConnectorWorker connectorWorker)
        {
            _logger = logger;
            _connector = connectorWorker;
            _connector.OnAssetSampleable += OnAssetSampleable;
            _connector.OnAssetNotSampleable += OnAssetNotSampleable;
        }

        public void OnAssetNotSampleable(object? sender, AssetUnavailabileEventArgs args)
        {
            _logger.LogInformation("Asset with name {0} is no longer sampleable", args.AssetName);
        }

        public async void OnAssetSampleable(object? sender, AssetAvailabileEventArgs args)
        {
            _logger.LogInformation("Asset with name {0} is now sampleable", args.AssetName);

            if (args.Asset.Datasets != null)
            {
                foreach (Dataset dataset in args.Asset.Datasets)
                { 
                    // Once a asset is available to be sampled, use the connector to sample its datasets from this thread or other threads
                    await _connector.SampleDatasetAsync(args.AssetName, args.Asset, dataset.Name);
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Implement your logic here
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}