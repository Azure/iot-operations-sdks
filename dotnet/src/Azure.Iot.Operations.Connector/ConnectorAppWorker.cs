using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.Assets;
using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.Connector
{
    public class ConnectorAppWorker : BackgroundService
    {
        private readonly ILogger<ConnectorAppWorker> _logger;
        private MqttSessionClient _sessionClient;
        private IDatasetSamplerFactory _datasetSamplerFactory;
        private ConcurrentDictionary<string, IDatasetSampler> _datasetSamplers = new();
        private AssetMonitor _adrClient;

        private Dictionary<string, Asset> _assets = new();
        private AssetEndpointProfile? _assetEndpointProfile;

        // Mapping of asset name to the dictionary that maps a dataset name to its sampler
        private Dictionary<string, Dictionary<string, Timer>> _samplers = new();

        public ConnectorAppWorker(ILogger<ConnectorAppWorker> logger, MqttSessionClient mqttSessionClient, IDatasetSamplerFactory datasetSamplerFactory)
        {
            _logger = logger;
            _sessionClient = mqttSessionClient;
            _datasetSamplerFactory = datasetSamplerFactory;
            _adrClient = new();
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                _adrClient.AssetEndpointProfileChanged += (sender, args) =>
                {
                    //TODO only start observing assets once an AEP is detected?
                    _logger.LogInformation("Recieved a notification that the asset endpoint definition has changed.");
                    _assetEndpointProfile = args.AssetEndpointProfile;
                };

                _adrClient.ObserveAssetEndpointProfile(null, cancellationToken);

                _logger.LogInformation("Successfully retrieved asset endpoint profile");

                // Create MQTT client from credentials provided by the operator
                MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();
                _logger.LogInformation($"Connecting to MQTT broker with {mqttConnectionSettings}");

                await _sessionClient.ConnectAsync(mqttConnectionSettings, cancellationToken);

                _logger.LogInformation($"Successfully connected to MQTT broker");

                _adrClient.AssetChanged += (sender, args) =>
                {
                    if (args.ChangeType == ChangeType.Deleted)
                    {
                        StopSamplingAsset(args.AssetName);
                    }
                    else if (args.ChangeType == ChangeType.Created)
                    {
                        _logger.LogInformation($"Recieved a notification an asset with name {args.AssetName} has been created.");
                        StartSamplingAsset(args.Asset!, cancellationToken);
                    }
                    else
                    {
                        _logger.LogInformation($"Recieved a notification the asset with name {args.AssetName} has been updated.");
                        _assets[args.AssetName] = args.Asset!;
                    }
                };

                _adrClient.ObserveAssets(null, cancellationToken);

                // Wait until the worker is cancelled
                await Task.Delay(-1, cancellationToken);
            }
            finally
            {
                _logger.LogInformation("Shutting down sample...");

                foreach (Dictionary<string, Timer> datasetSamplers in _samplers.Values)
                {
                    foreach (Timer datasetSampler in datasetSamplers.Values)
                    {
                        datasetSampler.Dispose();
                    }
                }

                _samplers.Clear();

                _adrClient.UnobserveAssets();

                _adrClient.UnobserveAssetEndpointProfile();
            }
        }

        private void StopSamplingAsset(string assetName)
        {
            _logger.LogInformation($"Recieved a notification the asset with name {assetName} has been deleted.");

            // Stop sampling this asset since it was deleted
            foreach (Timer datasetSampler in _samplers[assetName].Values)
            {
                datasetSampler.Dispose();
            }

            _samplers.Remove(assetName);
            _assets.Remove(assetName);
        }

        private void StartSamplingAsset(Asset asset, CancellationToken cancellationToken = default)
        {
            string assetName = asset.DisplayName!;
            _logger.LogInformation($"Discovered asset with name {assetName}");

            if (asset == null)
            {
                return;
            }

            _assets.Add(assetName, asset);

            _samplers[assetName] = new();
            if (_assets[assetName].DatasetsDictionary == null)
            {
                _logger.LogInformation($"Asset with name {assetName} has no datasets to sample");
                return;
            }
            else
            { 
                foreach (string datasetName in _assets[assetName].DatasetsDictionary!.Keys)
                {
                    Dataset dataset = _assets[assetName].DatasetsDictionary![datasetName];

                    TimeSpan defaultSamplingInterval = TimeSpan.FromMilliseconds(_assets[assetName].DefaultDatasetsConfiguration!.RootElement.GetProperty("samplingInterval").GetInt16());

                    TimeSpan samplingInterval = defaultSamplingInterval;
                    if (dataset.DatasetConfiguration != null
                        && dataset.DatasetConfiguration.RootElement.TryGetProperty("samplingInterval", out JsonElement datasetSpecificSamplingInterval))
                    {
                        samplingInterval = TimeSpan.FromMilliseconds(datasetSpecificSamplingInterval.GetInt16());
                    }

                    _logger.LogInformation($"Will sample dataset with name {datasetName} on asset with name {assetName} at a rate of once per {(int)samplingInterval.TotalMilliseconds} milliseconds");
                    Timer datasetSamplingTimer = new(SampleDataset, new DatasetSamplerContext(assetName, datasetName), 0, (int)samplingInterval.TotalMilliseconds);
                    _samplers[assetName][datasetName] = datasetSamplingTimer;

                    string mqttMessageSchema = dataset.GetMqttMessageSchema();
                    _logger.LogInformation($"Derived the schema for dataset with name {datasetName} in asset with name {assetName}:");
                    _logger.LogInformation(mqttMessageSchema);
                }
            }
        }

        private async void SampleDataset(object? status)
        {
            DatasetSamplerContext samplerContext = (DatasetSamplerContext)status!;

            string assetName = samplerContext.AssetName;
            string datasetName = samplerContext.DatasetName;

            Asset? asset = _assets[assetName];
            if (asset == null)
            {
                _logger.LogInformation($"Asset with name {assetName} was deleted. This sample won't sample its data.");
                return;
            }

            Dictionary<string, Dataset>? assetDatasets = asset.DatasetsDictionary;
            if (assetDatasets == null || !assetDatasets.ContainsKey(datasetName))
            {
                _logger.LogInformation($"Dataset with name {datasetName} in asset with name {samplerContext.AssetName} was deleted. This sample won't sample this dataset anymore.");
                return;
            }

            Dataset dataset = assetDatasets[datasetName];

            if (!_datasetSamplers.ContainsKey(datasetName))
            {
                _datasetSamplers.TryAdd(datasetName, _datasetSamplerFactory.CreateDatasetSampler(_assetEndpointProfile!, asset, dataset));
            }

            if (!_datasetSamplers.TryGetValue(datasetName, out IDatasetSampler? datasetSampler))
            {
                _logger.LogInformation($"Dataset with name {datasetName} in asset with name {samplerContext.AssetName} was deleted. This sample won't sample this dataset anymore.");
                return;
            }

            byte[] serializedPayload = await datasetSampler.SampleDatasetAsync(dataset);

            _logger.LogInformation($"Read dataset with name {dataset.Name} from asset with name {assetName}. Now publishing it to MQTT broker: {Encoding.UTF8.GetString(serializedPayload)}");

            var topic = dataset.Topic != null ? dataset.Topic! : asset.DefaultTopic!;
            var mqttMessage = new MqttApplicationMessage(topic.Path!)
            {
                PayloadSegment = serializedPayload,
                Retain = topic.Retain == RetainHandling.Keep,
            };

            var puback = await _sessionClient.PublishAsync(mqttMessage);

            if (puback.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                _logger.LogInformation($"Received successful PUBACK from MQTT broker: {puback.ReasonCode} with reason {puback.ReasonString}");
            }
            else
            {
                // There is no consumer of these messages yet, so NoMatchingSubscribers error is expected here
                _logger.LogInformation($"Received unsuccessful PUBACK from MQTT broker: {puback.ReasonCode} with reason {puback.ReasonString}");
            }
        }
    }
}