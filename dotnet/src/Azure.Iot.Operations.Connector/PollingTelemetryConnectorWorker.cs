// Copyright(c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Assets;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Iot.Operations.Connector
{
    public class PollingTelemetryConnectorWorker : ConnectorWorker
    {
        private readonly Dictionary<string, Dictionary<string, Timer>> _assetsSamplingTimers = new();
        private readonly IDatasetSamplerFactory _datasetSamplerFactory;

        public PollingTelemetryConnectorWorker(ApplicationContext applicationContext, ILogger<ConnectorWorker> logger, IMqttClient mqttClient, IDatasetSamplerFactory datasetSamplerFactory, IMessageSchemaProvider messageSchemaFactory, IAdrClientWrapper assetMonitor, IConnectorLeaderElectionConfigurationProvider? leaderElectionConfigurationProvider = null) : base(applicationContext, logger, mqttClient, messageSchemaFactory, assetMonitor, leaderElectionConfigurationProvider)
        {
            base.OnAssetAvailable += OnAssetSampleableAsync;
            base.OnAssetUnavailable += OnAssetNotSampleableAsync;
            _datasetSamplerFactory = datasetSamplerFactory;
        }

        public void OnAssetNotSampleableAsync(object? sender, AssetUnavailableEventArgs args)
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

        public async void OnAssetSampleableAsync(object? sender, AssetAvailableEventArgs args)
        {
            if (args.Asset.Datasets == null)
            {
                return;
            }

            Dictionary<string, Timer> datasetsTimers = new();
            _assetsSamplingTimers[args.AssetName] = datasetsTimers;
            foreach (AssetDataset dataset in args.Asset.Datasets!)
            {
                EndpointCredentials? credentials = null;
                if (args.Device.Endpoints != null
                    && args.Device.Endpoints.Inbound != null
                    && args.Device.Endpoints.Inbound.TryGetValue(args.InboundEndpointName, out var inboundEndpoint))
                {
                    credentials = _assetMonitor.GetEndpointCredentials(inboundEndpoint);
                }

                IDatasetSampler datasetSampler = _datasetSamplerFactory.CreateDatasetSampler(args.Device, args.InboundEndpointName, args.AssetName, args.Asset, dataset, credentials);

                TimeSpan samplingInterval = await datasetSampler.GetSamplingIntervalAsync(dataset);

                _logger.LogInformation("Dataset with name {0} in asset with name {1} will be sampled once every {2} milliseconds", dataset.Name, args.AssetName, samplingInterval.TotalMilliseconds);

                var datasetSamplingTimer = new Timer(async (state) =>
                {
                    try
                    {
                        byte[] sampledData = await datasetSampler.SampleDatasetAsync(dataset);
                        await ForwardSampledDatasetAsync(args.Asset, dataset, sampledData);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to sample the dataset");
                    }
                }, null, TimeSpan.FromSeconds(0), samplingInterval);

                if (!datasetsTimers.TryAdd(dataset.Name, datasetSamplingTimer))
                {
                    _logger.LogError("Failed to save dataset sampling timer for asset with name {} for dataset with name {}", args.AssetName, dataset.Name);
                }
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
