// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.Assets;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Azure.Iot.Operations.Connector
{
    public class PollingTelemetryConnectorWorker : EventDrivenTelemetryConnectorWorker
    {
        Dictionary<string, Dictionary<string, Timer>> _assetsSamplingTimers = new();

        public PollingTelemetryConnectorWorker(ILogger<EventDrivenTelemetryConnectorWorker> logger, IMqttClient mqttClient, IDatasetSamplerFactory datasetSamplerFactory, IAssetMonitor assetMonitor) : base(logger, mqttClient, datasetSamplerFactory, assetMonitor)
        {
            base.OnAssetAvailable += OnAssetSampleableAsync;
            base.OnAssetUnavailable += OnAssetNotSampleableAsync;
        }

        public void OnAssetNotSampleableAsync(object? sender, AssetUnavailabileEventArgs args)
        {
            if (_assetsSamplingTimers.Remove(args.AssetName, out Dictionary<string, Timer>? datasetTimers) && datasetTimers != null)
            {
                foreach (string datasetName in datasetTimers.Keys)
                {
                    Timer timer = datasetTimers[datasetName];
                    _logger.LogInformation("Dataset with name {0} in asset with name {1} will no longer be periodically sampled", datasetName, args.AssetName);
                    timer.Dispose();
                }
            }
        }

        public void OnAssetSampleableAsync(object? sender, AssetAvailabileEventArgs args)
        {
            if (args.Asset.Datasets == null)
            {
                return;
            }

            _assetsSamplingTimers[args.AssetName] = new Dictionary<string, Timer>();
            
            foreach (Dataset dataset in args.Asset.Datasets)
            {
                TimeSpan samplingInterval;
                if (dataset.DatasetConfiguration != null
                    && dataset.DatasetConfiguration.RootElement.TryGetProperty("samplingInterval", out JsonElement datasetSpecificSamplingInterval)
                    && datasetSpecificSamplingInterval.TryGetInt32(out int datasetSpecificSamplingIntervalMilliseconds))
                {
                    samplingInterval = TimeSpan.FromMilliseconds(datasetSpecificSamplingIntervalMilliseconds);
                }
                else if (args.Asset.DefaultDatasetsConfiguration != null
                    && args.Asset.DefaultDatasetsConfiguration.RootElement.TryGetProperty("samplingInterval", out JsonElement defaultDatasetSamplingInterval)
                    && defaultDatasetSamplingInterval.TryGetInt32(out int defaultSamplingIntervalMilliseconds))
                {
                    samplingInterval = TimeSpan.FromMilliseconds(defaultSamplingIntervalMilliseconds);
                }
                else
                {
                    _logger.LogError($"Dataset with name {dataset.Name} in Asset with name {args.AssetName} has no configured sampling interval. This dataset will not be sampled.");
                    return;
                }

                _logger.LogInformation("Dataset with name {0} in asset with name {1} will be sampled once every {2} milliseconds", dataset.Name, args.AssetName, samplingInterval.TotalMilliseconds);

                _assetsSamplingTimers[args.AssetName][dataset.Name] = new Timer(async (state) =>
                {
                    await SampleDatasetAsync(args.AssetName, args.Asset, dataset.Name);
                }, null, TimeSpan.FromSeconds(0), samplingInterval);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            foreach (var assetName in _assetsSamplingTimers.Keys)
            {
                foreach (var datasetName in _assetsSamplingTimers[assetName].Keys)
                {
                    _assetsSamplingTimers[assetName][datasetName].Dispose();
                }
            }
        }
    }
}