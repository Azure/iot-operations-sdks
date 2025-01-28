// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public class RestThermostatEventingConnectorWorker : EventingTelemetryConnectorWorker, IDisposable
    {
        private SemaphoreSlim _assetSemaphore = new(1);
        Dictionary<string, Asset> _sampleableAssets = new Dictionary<string, Asset>();

        public RestThermostatEventingConnectorWorker(ILogger<EventingTelemetryConnectorWorker> logger, IMqttClient mqttClient, IDatasetSamplerFactory datasetSamplerFactory, IAssetMonitor assetMonitor) : base(logger, mqttClient, datasetSamplerFactory, assetMonitor)
        {
        }

        public override Task OnAssetNotSampleableAsync(string assetName, CancellationToken cancellationToken)
        {
            _assetSemaphore.Wait(cancellationToken);
            try
            {
                if (_sampleableAssets.Remove(assetName, out Asset? asset))
                {
                    _logger.LogInformation("Asset with name {0} is no longer sampleable", asset.DisplayName);
                }
            }
            finally
            { 
                _assetSemaphore.Release();
            }

            return Task.CompletedTask;
        }

        public override Task OnAssetSampleableAsync(string assetName, Asset asset, CancellationToken cancellationToken)
        {
            _assetSemaphore.Wait(cancellationToken);
            try
            {
                if (_sampleableAssets.TryAdd(assetName, asset))
                {
                    _logger.LogInformation("Asset with name {0} is now sampleable", asset.DisplayName);
                }
            }
            finally
            {
                _assetSemaphore.Release();
            }

            return Task.CompletedTask;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Run the base class's loop in another thread so that this thread can act independently
            _ = base.ExecuteAsync(cancellationToken);
            await RunEventingLoopAsync(cancellationToken);
        }

        // This method simulates an unrelated thread that occasionally samples the available datasets.
        private async Task RunEventingLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(new Random().Next(1000, 5000), cancellationToken);

                try
                {
                    foreach (string assetName in _sampleableAssets.Keys)
                    {
                        Asset sampleableAsset = _sampleableAssets[assetName];
                        if (sampleableAsset.Datasets == null) 
                        {
                            continue;
                        }

                        foreach (Dataset dataset in sampleableAsset.Datasets)
                        {
                            try
                            {
                                await base.SampleDatasetAsync(assetName, sampleableAsset, dataset.Name);
                            }
                            catch (AssetDatasetUnavailableException e)
                            {
                                // This may happen if you try to sample a dataset when its asset was just deleted
                                _logger.LogWarning(e, "Failed to sample dataset with name {0} on asset with name {1} because it is no longer sampleable", dataset.Name, assetName);
                            }
                            catch (ConnectorSamplingException e)
                            {
                                // This may happen if the asset (an HTTP server in this sample's case) failed to respond to a request or otherwise could not be reached.
                                _logger.LogWarning(e, "Failed to sample dataset with name {0} on asset with name {1} because the asset could not be reached", dataset.Name, assetName);
                            }
                            catch (ConnectorException e)
                            {
                                _logger.LogWarning(e, "Failed to sample dataset with name {0} on asset with name {1}", dataset.Name, assetName);
                            }
                        }
                    }
                }
                finally
                {
                    _assetSemaphore.Release();
                }
            }
        }

        public override void Dispose()
        { 
            _assetSemaphore.Dispose();
        }
    }
}