using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.Assets;
using Azure.Iot.Operations.Services.LeaderElection;
using Azure.Iot.Operations.Services.SchemaRegistry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.Connector
{
    public class PollingTelemetryConnectorWorker : EventingTelemetryConnectorWorker
    {
        Dictionary<string, Dictionary<string, Timer>> _assetsSamplingTimers = new();

        public PollingTelemetryConnectorWorker(ILogger<EventingTelemetryConnectorWorker> logger, IMqttClient mqttClient, IDatasetSamplerFactory datasetSamplerFactory, IAssetMonitor assetMonitor) : base(logger, mqttClient, datasetSamplerFactory, assetMonitor)
        {
        }

        public override Task OnAssetNotSampleableAsync(string assetName, CancellationToken cancellationToken)
        {
            if (_assetsSamplingTimers.Remove(assetName, out Dictionary<string, Timer>? datasetTimers) && datasetTimers != null)
            {
                foreach (string datasetName in datasetTimers.Keys)
                {
                    Timer timer = datasetTimers[datasetName];
                    _logger.LogInformation("Dataset with name {0} in asset with name {1} will no longer be periodically sampled", datasetName, assetName);
                    timer.Dispose();
                }
            }

            return Task.CompletedTask;
        }

        public override Task OnAssetSampleableAsync(string assetName, Asset asset, CancellationToken cancellationToken)
        {
            if (asset.Datasets == null)
            {
                return Task.CompletedTask;
            }

            _assetsSamplingTimers[assetName] = new Dictionary<string, Timer>();
            
            foreach (Dataset dataset in asset.Datasets)
            {
                TimeSpan samplingInterval;
                if (dataset.DatasetConfiguration != null
                    && dataset.DatasetConfiguration.RootElement.TryGetProperty("samplingInterval", out JsonElement datasetSpecificSamplingInterval)
                    && datasetSpecificSamplingInterval.TryGetInt32(out int datasetSpecificSamplingIntervalMilliseconds))
                {
                    samplingInterval = TimeSpan.FromMilliseconds(datasetSpecificSamplingIntervalMilliseconds);
                }
                else if (asset.DefaultDatasetsConfiguration != null
                    && asset.DefaultDatasetsConfiguration.RootElement.TryGetProperty("samplingInterval", out JsonElement defaultDatasetSamplingInterval)
                    && defaultDatasetSamplingInterval.TryGetInt32(out int defaultSamplingIntervalMilliseconds))
                {
                    samplingInterval = TimeSpan.FromMilliseconds(defaultSamplingIntervalMilliseconds);
                }
                else
                {
                    _logger.LogError($"Dataset with name {dataset.Name} in Asset with name {assetName} has no configured sampling interval. This dataset will not be sampled.");
                    return Task.CompletedTask;
                }

                _logger.LogInformation("Dataset with name {0} in asset with name {1} will be sampled once every {2} milliseconds", dataset.Name, assetName, samplingInterval.TotalMilliseconds);

                _assetsSamplingTimers[assetName][dataset.Name] = new Timer(async (state) =>
                {
                    await SampleDatasetAsync(assetName, asset, dataset.Name, cancellationToken);
                }, null, TimeSpan.FromSeconds(0), samplingInterval);
            }

            return Task.CompletedTask;
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