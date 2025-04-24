// Copyright(c) Microsoft Corporation.
// Licensed under the MIT License.

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
            if (args.Asset.Specification.Datasets == null)
            {
                return;
            }

            _assetsSamplingTimers[args.AssetName] = new Dictionary<string, Timer>();
            foreach (AssetDatasetSchemaElement dataset in args.Asset.Specification.Datasets)
            {
                IDatasetSampler datasetSampler = _datasetSamplerFactory.CreateDatasetSampler(args.Device, args.InboundEndpointName, args.Asset, dataset, _assetMonitor.GetDeviceCredentials(args.Device.Name, args.InboundEndpointName));

                TimeSpan samplingInterval = await datasetSampler.GetSamplingIntervalAsync(dataset);

                _logger.LogInformation("Dataset with name {0} in asset with name {1} will be sampled once every {2} milliseconds", dataset.Name, args.AssetName, samplingInterval.TotalMilliseconds);

                _assetsSamplingTimers[args.AssetName][dataset.Name] = new Timer(async (state) =>
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
