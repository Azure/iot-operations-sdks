// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using System.Collections.Concurrent;

namespace RestThermostatConnector
{
    public class AssetNotificationHandler : IAssetNotificationHandler
    {
        public static Func<IServiceProvider, IAssetNotificationHandler> Provider = service =>
        {
            return new AssetNotificationHandler();
        };

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
