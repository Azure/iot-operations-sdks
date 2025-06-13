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
            base.WhileAssetIsAvailable = WhileAssetAvailableAsync;
            _datasetSamplerFactory = datasetSamplerFactory;
        }

        public async Task WhileAssetAvailableAsync(AssetAvailableEventArgs args, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

            // Waits until the asset is no longer available
            cancellationToken.WaitHandle.WaitOne();

            // Stop sampling all datasets in this asset now that the asset is unavailable
            foreach (AssetDataset dataset in args.Asset.Datasets!)
            {
                _logger.LogInformation("Dataset with name {0} in asset with name {1} will no longer be periodically sampled", dataset.Name, args.AssetName);
                _assetsSamplingTimers[args.AssetName][dataset.Name].Dispose();
            }
        }
    }
}
