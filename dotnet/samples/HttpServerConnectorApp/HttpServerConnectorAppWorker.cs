using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace HttpServerConnectorApp
{
    public class HttpServerConnectorAppWorker : BackgroundService
    {
        private bool doSchemaWork = false;
        private readonly ILogger<HttpServerConnectorAppWorker> _logger;
        private MqttSessionClient _sessionClient;
        private IDatasetSourceFactory _datasetSamplerFactory;
        private ConcurrentDictionary<string, IDatasetSource> _datasetSamplers = new();
        private SchemaRegistryClient _schemaRegistryClient;
        private AzureDeviceRegistryClient _adrClient;

        private Dictionary<string, Asset> _assets = new();
        private AssetEndpointProfile? _assetEndpointProfile;
        
        // Mapping of asset name to the dictionary that maps a dataset name to its sampler
        private Dictionary<string, Dictionary<string, Timer>> _samplers = new();

        public HttpServerConnectorAppWorker(ILogger<HttpServerConnectorAppWorker> logger, MqttSessionClient mqttSessionClient, IDatasetSourceFactory datasetSamplerFactory)
        {
            _logger = logger;
            _sessionClient = mqttSessionClient;
            _datasetSamplerFactory = datasetSamplerFactory;
            _schemaRegistryClient = new(_sessionClient);
            _adrClient = new();
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                _assetEndpointProfile = await _adrClient.GetAssetEndpointProfileAsync(cancellationToken);

                if (_assetEndpointProfile == null)
                {
                    throw new InvalidOperationException("Missing asset endpoint profile configuration");
                }

                _adrClient.AssetEndpointProfileChanged += (sender, args) =>
                {
                    _logger.LogInformation("Recieved a notification that the asset endpoint definition has changed.");
                    _assetEndpointProfile = args.AssetEndpointProfile;
                };

                await _adrClient.ObserveAssetEndpointProfileAsync(null, cancellationToken);

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
                        _logger.LogInformation($"Recieved a notification the asset with name {args.AssetName} has been deleted.");

                        // Stop sampling this asset since it was deleted
                        foreach (Timer datasetSampler in _samplers[args.AssetName].Values)
                        {
                            datasetSampler.Dispose();
                        }

                        _samplers.Remove(args.AssetName);
                        _assets.Remove(args.AssetName);
                    }
                    else if (args.ChangeType == ChangeType.Created)
                    {
                        _logger.LogInformation($"Recieved a notification an asset with name {args.AssetName} has been created.");

                        _ = StartSamplingAssetAsync(args.AssetName, cancellationToken);
                    }
                    else
                    {
                        _logger.LogInformation($"Recieved a notification the asset with name {args.AssetName} has been updated.");
                        _assets[args.AssetName] = args.Asset!;
                    }
                };

                await _adrClient.ObserveAssetsAsync(null, cancellationToken);

                bool assetFound = false;
                foreach (string assetName in await _adrClient.GetAssetNamesAsync(cancellationToken))
                {
                    _logger.LogInformation($"Initial discovered assetname: {assetName}");
                    await StartSamplingAssetAsync(assetName, cancellationToken);
                    assetFound = true;
                }

                if (!assetFound)
                {
                    _logger.LogInformation($"No assets discovered on startup.");
                }

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

                await _adrClient.UnobserveAssetsAsync();

                await _adrClient.UnobserveAssetEndpointProfileAsync();
            }
        }

        private async Task StartSamplingAssetAsync(string assetName, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"Discovered asset with name {assetName}");
            Asset? asset = await _adrClient.GetAssetAsync(assetName, cancellationToken);

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
                    Timer datasetSamplingTimer = new(SampleDataset, new DatasetSourceContext(assetName, datasetName), 0, (int)samplingInterval.TotalMilliseconds);
                    _samplers[assetName][datasetName] = datasetSamplingTimer;

                    string mqttMessageSchema = dataset.GetMqttMessageSchema();
                    _logger.LogInformation($"Derived the schema for dataset with name {datasetName} in asset with name {assetName}:");
                    _logger.LogInformation(mqttMessageSchema);

                    if (doSchemaWork)
                    {
                        var schema = await _schemaRegistryClient.PutAsync(
                            mqttMessageSchema,
                            Enum_Ms_Adr_SchemaRegistry_Format__1.JsonSchemaDraft07,
                            Enum_Ms_Adr_SchemaRegistry_SchemaType__1.MessageSchema,
                            "1.0.0", //TODO version?
                        new(),
                            null,
                            cancellationToken);

                        if (schema == null)
                        {
                            throw new InvalidOperationException("Failed to register the message schema with the schema registry service");
                        }

                        asset.Status ??= new();
                        asset.Status.Events ??= new StatusEvents[1]; //TODO more status events later if asset changes?
                        asset.Status.Events[0] = new StatusEvents()
                        {
                            Name = schema.Name,
                            MessageSchemaReference = new()
                            {
                                SchemaName = schema.Name,
                                SchemaRegistryNamespace = schema.Namespace,
                                SchemaVersion = schema.Version,
                            }
                        };
                    }
                }
            }
        }

        private async void SampleDataset(object? status)
        {
            DatasetSourceContext samplerContext = (DatasetSourceContext)status!;

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
                _datasetSamplers.TryAdd(datasetName, _datasetSamplerFactory.CreateDatasetSource(_assetEndpointProfile!, asset, dataset));
            }

            if (!_datasetSamplers.TryGetValue(datasetName, out IDatasetSource? datasetSampler))
            {
                _logger.LogInformation($"Dataset with name {datasetName} in asset with name {samplerContext.AssetName} was deleted. This sample won't sample this dataset anymore.");
                return;
            }

            byte[] serializedPayload = await datasetSampler.SampleAsync(dataset);

            _logger.LogInformation($"Read dataset with name {dataset.Name} from asset with name {assetName}. Now publishing it to MQTT broker: {Encoding.UTF8.GetString(serializedPayload)}");

            var topic = dataset.Topic != null ? dataset.Topic! : asset.DefaultTopic!;
            var mqttMessage = new MqttApplicationMessage(topic.Path!)
            {
                PayloadSegment = serializedPayload,
                Retain = topic.Retain == RetainHandling.Keep,
            };

            if (asset.Status != null
                && asset.Status.DatasetsDictionary != null
                && asset.Status.DatasetsDictionary[datasetName] != null
                && asset.Status.DatasetsDictionary[datasetName].MessageSchemaReference != null)
            {
                _logger.LogInformation("Message schema configured, will include cloud event headers");
                var messageSchemaReference = asset.Status.DatasetsDictionary[datasetName].MessageSchemaReference!;

                mqttMessage.AddCloudEvents(
                    new CloudEvent(
                        new Uri(_assetEndpointProfile!.TargetAddress),
                        messageSchemaReference.SchemaRegistryNamespace + messageSchemaReference.SchemaName,
                        messageSchemaReference.SchemaVersion));
            }

            var puback = await _sessionClient.PublishAsync(mqttMessage);

            if (puback.ReasonCode != MqttClientPublishReasonCode.Success
                && puback.ReasonCode != MqttClientPublishReasonCode.NoMatchingSubscribers) // There is no consumer of these messages yet, so ignore this expected NoMatchingSubscribers error
            {
                _logger.LogInformation($"Received unsuccessful PUBACK from MQTT broker: {puback.ReasonCode} with reason {puback.ReasonString}");
            }
        }
    }
}