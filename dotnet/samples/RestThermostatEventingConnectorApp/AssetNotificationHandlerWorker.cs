// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using System.Collections.Concurrent;

namespace RestThermostatConnector
{
    public class AssetNotificationHandlerWorker : BackgroundService, IAssetNotificationHandler
    {
        private IDictionary<string, SampleableAsset> sampleableAssets = new ConcurrentDictionary<string, SampleableAsset>();
        private readonly ILogger<AssetNotificationHandlerWorker> _logger;

        public AssetNotificationHandlerWorker(ILogger<AssetNotificationHandlerWorker> logger)
        {
            _logger = logger;
        }

        public Task OnAssetNotSampleable(string assetName)
        {
            _logger.LogInformation($"Asset {assetName} is no longer sampleable");
            sampleableAssets.Remove(assetName);
            return Task.CompletedTask;
        }

        public Task OnAssetSampleable(SampleableAsset sampleableAsset)
        {
            _logger.LogInformation($"Asset {sampleableAsset.Asset.DisplayName} is sampleable");
            sampleableAssets.TryAdd(sampleableAsset.Asset.DisplayName!, sampleableAsset);
            return Task.CompletedTask;
        }

        public async Task SampleAvailableAssetsAsync(CancellationToken cancellationToken = default)
        {
            foreach (var sampleableAsset in sampleableAssets.Values)
            {
                if (sampleableAsset.Asset.Datasets != null)
                {
                    foreach (var dataset in sampleableAsset.Asset.Datasets)
                    {
                        try
                        {
                            await sampleableAsset.SampleDatasetAsync(dataset.Name, cancellationToken);
                        }
                        catch (AssetDatasetUnavailableException e)
                        {
                            // Tried to sample a dataset that either didn't exist or the asset it belongs to is not available anymore
                            _logger.LogInformation(e, $"Cannot currently sample dataset {dataset.Name} on asset {sampleableAsset.Asset.DisplayName}");
                        }
                        catch (ConnectorSamplingException e)
                        {
                            // Tried to sample a dataset, but an error happened when connecting to or reading from the asset (HTTP connect timeout, for example)
                            _logger.LogInformation(e, $"Failed to sample dataset {dataset.Name} on asset {sampleableAsset.Asset.DisplayName}");
                        }
                    }
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.MaxValue, stoppingToken);
            }
        }
    }
}
