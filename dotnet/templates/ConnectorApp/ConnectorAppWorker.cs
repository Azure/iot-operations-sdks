using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace ConnectorAppProjectTemplate
{
    public class ConnectorAppWorker : BackgroundService
    {
        private bool doSchemaWork = false;
        private readonly ILogger<ConnectorAppWorker> _logger;
        private MqttSessionClient _sessionClient;
        private IDatasetSamplerFactory _datasetSamplerFactory;
        private Dictionary<string, IDatasetSampler> _datasetSamplers = new();

        private Dictionary<string, Asset> _assets = new();
        private AssetEndpointProfile? _assetEndpointProfile;

        public ConnectorAppWorker(ILogger<ConnectorAppWorker> logger, MqttSessionClient mqttSessionClient, IDatasetSamplerFactory datasetSamplerFactory)
        {
            _logger = logger;
            _sessionClient = mqttSessionClient;
            _datasetSamplerFactory = datasetSamplerFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            AzureDeviceRegistryClient adrClient = new();
            SchemaRegistryClient schemaRegistryClient = new(_sessionClient);

            //TODO once schema registry client is ready, connector should register the schema on startup. The connector then puts the schema in the asset status field.
            // Additionally, the telemetry sent by this connector should be stamped as a cloud event

            List<Timer> samplers = new List<Timer>();

            try
            {
                _assetEndpointProfile = await adrClient.GetAssetEndpointProfileAsync(cancellationToken);

                if (_assetEndpointProfile == null)
                {
                    throw new InvalidOperationException("Missing asset endpoint profile configuration");
                }

                adrClient.AssetEndpointProfileChanged += (sender, newAssetEndpointProfile) =>
                {
                    _logger.LogInformation("Recieved a notification that the asset endpoint definition has changed.");
                    _assetEndpointProfile = newAssetEndpointProfile;
                };

                await adrClient.ObserveAssetEndpointProfileAsync(null, cancellationToken);

                _logger.LogInformation("Successfully retrieved asset endpoint profile");

                // Create MQTT client from credentials provided by the operator
                MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();
                mqttConnectionSettings.TcpPort = 18883; //TODO configurable?
                _logger.LogInformation($"Connecting to MQTT broker with {mqttConnectionSettings}");

                await _sessionClient.ConnectAsync(mqttConnectionSettings, cancellationToken);

                _logger.LogInformation($"Successfully connected to MQTT broker");

                foreach (string assetName in await adrClient.GetAssetNamesAsync(cancellationToken))
                {
                    _logger.LogInformation($"Discovered asset with name {assetName}");
                    Asset? asset = await adrClient.GetAssetAsync(assetName, cancellationToken);

                    if (asset == null)
                    {
                        continue;
                    }

                    _assets.Add(assetName, asset);

                    adrClient.AssetChanged += (sender, newAsset) =>
                    {
                        if (newAsset == null)
                        {
                            _logger.LogInformation($"Recieved a notification the asset with name {assetName} has been deleted.");
                            _assets.Remove(assetName);
                        }
                        else
                        {
                            _logger.LogInformation($"Recieved a notification the asset with name {assetName} has been updated.");
                            _assets[assetName] = newAsset;
                        }
                    };

                    await adrClient.ObserveAssetAsync(assetName, null, cancellationToken);

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
                        Timer datasetSamplingTimer = new(SampleDataset, new SamplerContext(assetName, datasetName), 0, (int)samplingInterval.TotalMilliseconds);
                        samplers.Add(datasetSamplingTimer);

                        string mqttMessageSchema = dataset.GetMqttMessageSchema();
                        _logger.LogInformation($"Derived the schema for dataset with name {datasetName} in asset with name {assetName}:");
                        _logger.LogInformation(mqttMessageSchema);

                        if (doSchemaWork)
                        {
                            var schema = await schemaRegistryClient.PutAsync(
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

                // Wait until the worker is cancelled
                await Task.Delay(-1, cancellationToken);
            }
            finally
            {
                _logger.LogInformation("Shutting down sample...");

                foreach (Timer sampler in samplers)
                {
                    sampler.Dispose();
                }

                foreach (string assetName in _assets.Keys)
                {
                    await adrClient.UnobserveAssetAsync(assetName);
                }

                await adrClient.UnobserveAssetEndpointProfileAsync();
            }
        }

        private async void SampleDataset(object? status)
        {
            SamplerContext samplerContext = (SamplerContext)status!;

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
                _datasetSamplers[datasetName] = _datasetSamplerFactory.CreateDatasetSampler(_assetEndpointProfile!, dataset);
            }

            byte[] serializedPayload = await _datasetSamplers[datasetName].SampleAsync(dataset, _assetEndpointProfile!.Credentials);

            _logger.LogInformation($"Read dataset from asset. Now publishing it to MQTT broker: {Encoding.UTF8.GetString(serializedPayload)}");

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
