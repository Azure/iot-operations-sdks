// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;

namespace Azure.Iot.Operations.Connector
{
    public class EventingWorker : BackgroundService, IAssetNotificationHandler
    {
        private readonly ILogger<EventingWorker> _logger;

        public EventingWorker(ILogger<EventingWorker> logger)
        {
            _logger = logger;
        }

        private IDictionary<string, SampleableAsset> sampleableAssets = new ConcurrentDictionary<string, SampleableAsset>();

        public Task OnAssetNotSampleable(string assetName)
        {
            sampleableAssets.Remove(assetName);
            return Task.CompletedTask;
        }

        public Task OnAssetSampleable(SampleableAsset sampleableAsset)
        {
            sampleableAssets.TryAdd(sampleableAsset.Asset.DisplayName!, sampleableAsset);
            return Task.CompletedTask;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Simulate randomly occurring events that trigger sampling of available assets
            while (!cancellationToken.IsCancellationRequested)
            { 
                await Task.Delay(new Random().Next(1000, 5000), cancellationToken);
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
                            }
                            catch (ConnectorSamplingException e)
                            {
                                // Tried to sample a dataset, but an error happened when connecting to or reading from the asset (HTTP connect timeout, for example)
                            }
                        }
                    }
                }
            }
        }
    }
}