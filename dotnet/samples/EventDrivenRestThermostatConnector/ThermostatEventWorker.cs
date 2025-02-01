// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public class ThermostatEventWorker : BackgroundService, IDisposable
    {
        private SemaphoreSlim _assetSemaphore = new(1);
        Dictionary<string, Asset> _sampleableAssets = new Dictionary<string, Asset>();
        private EventDrivenTelemetryConnectorWorker _connector;
        private ILogger<ThermostatEventWorker> _logger;

        public ThermostatEventWorker(ILogger<EventDrivenTelemetryConnectorWorker> connectorLogger,
            ILogger<ThermostatEventWorker> logger,
            IMqttClient mqttClient,
            IDatasetSamplerFactory datasetSamplerFactory,
            IAssetMonitor assetMonitor)
        {
            _logger = logger;
            _connector = new(connectorLogger, mqttClient, datasetSamplerFactory, assetMonitor);
            _connector.OnAssetSampleable += OnAssetSampleable;
            _connector.OnAssetNotSampleable += OnAssetNotSampleable;
        }

        public void OnAssetNotSampleable(object? sender, AssetUnavailabileEventArgs args)
        {
            _assetSemaphore.Wait();
            try
            {
                if (_sampleableAssets.Remove(args.AssetName, out Asset? asset))
                {
                    _logger.LogInformation("Asset with name {0} is no longer sampleable", asset.DisplayName);
                }
            }
            finally
            { 
                _assetSemaphore.Release();
            }
        }

        public void OnAssetSampleable(object? sender, AssetAvailabileEventArgs args)
        {
            _assetSemaphore.Wait();
            try
            {
                if (_sampleableAssets.TryAdd(args.AssetName, args.Asset))
                {
                    _logger.LogInformation("Asset with name {0} is now sampleable", args.AssetName);
                }
            }
            finally
            {
                _assetSemaphore.Release();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.WhenAny(
                _connector.StartAsync(cancellationToken), 
                EventLoopAsync(cancellationToken));
        }

        private async Task EventLoopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting event thread");
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(new Random().Next(1000, 5000), cancellationToken);

                _assetSemaphore.Wait(cancellationToken);
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
                                await _connector.SampleDatasetAsync(assetName, sampleableAsset, dataset.Name);
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

        public void Dispose()
        { 
            _assetSemaphore.Dispose();
        }
    }
}