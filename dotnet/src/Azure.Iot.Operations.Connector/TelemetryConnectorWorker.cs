// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Exceptions;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Azure.Iot.Operations.Services.Assets;
using Azure.Iot.Operations.Services.LeaderElection;
using Azure.Iot.Operations.Services.SchemaRegistry;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Base class for a connector worker that allows users to forward data samplied from datasets and forwarding of received events.
    /// </summary>
    public class TelemetryConnectorWorker : ConnectorBackgroundService
    {
        protected readonly ILogger<TelemetryConnectorWorker> _logger;
        private readonly IMqttClient _mqttClient;
        private readonly ApplicationContext _applicationContext;
        private readonly AdrClientWrapper _assetMonitor;
        private readonly IMessageSchemaProvider _messageSchemaProviderFactory;
        private readonly ConcurrentDictionary<string, Asset> _assets = new();
        private bool _isDisposed = false;

        /// <summary>
        /// Event handler for when an asset becomes available.
        /// </summary>
        public EventHandler<AssetAvailabileEventArgs>? OnAssetAvailable;

        /// <summary>
        /// Event handler for when an asset becomes unavailable.
        /// </summary>
        public EventHandler<AssetUnavailableEventArgs>? OnAssetUnavailable;

        /// <summary>
        /// The asset endpoint profile associated with this connector. This will be null until the asset endpoint profile is first discovered.
        /// </summary>
        public AssetEndpointProfile? AssetEndpointProfile { get; set; }

        private readonly ConnectorLeaderElectionConfiguration? _leaderElectionConfiguration;

        public TelemetryConnectorWorker(
            ApplicationContext applicationContext,
            ILogger<TelemetryConnectorWorker> logger,
            IMqttClient mqttClient,
            IMessageSchemaProvider messageSchemaProviderFactory,
            AdrClientWrapper assetMonitor,
            IConnectorLeaderElectionConfigurationProvider? leaderElectionConfigurationProvider = null)
        {
            _applicationContext = applicationContext;
            _logger = logger;
            _mqttClient = mqttClient;
            _messageSchemaProviderFactory = messageSchemaProviderFactory;
            _assetMonitor = assetMonitor;
            _leaderElectionConfiguration = leaderElectionConfigurationProvider?.GetLeaderElectionConfiguration();
        }

        ///<inheritdoc/>
        public override Task RunConnectorAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            // This method is public to allow users to access the BackgroundService interface's ExecuteAsync method.
            return ExecuteAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            string candidateName = Guid.NewGuid().ToString();

            // Create MQTT client from credentials provided by the operator
            MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();
            _logger.LogInformation("Connecting to MQTT broker with hostname {hostname} and port {port}", mqttConnectionSettings.HostName, mqttConnectionSettings.TcpPort);

            await _mqttClient.ConnectAsync(mqttConnectionSettings, cancellationToken);

            _logger.LogInformation($"Successfully connected to MQTT broker");

            _assetMonitor.AssetEndpointProfileChanged += (sender, args) =>
            {
                // Each connector should have one AEP deployed to the pod. It shouldn't ever be deleted, but it may be updated.
                if (args.ChangeType == ChangeType.Created)
                {
                    if (args.AssetEndpointProfile == null)
                    {
                        // shouldn't ever happen
                        _logger.LogError("Received notification that asset endpoint profile was created, but no asset endpoint profile was provided");
                    }
                    else
                    {
                    }
                }
                else if (args.ChangeType == ChangeType.Deleted)
                {
                }
                else if (args.ChangeType == ChangeType.Updated)
                {
                }
            };

            _assetMonitor.Start();

            try
            {
                await Task.Delay(TimeSpan.MaxValue, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Connector app was cancelled. Shutting down now.");
            }

            await _assetMonitor.StopAsync();
            await _mqttClient.DisconnectAsync();
        }

        private async Task AssetAvailableAsync(AssetEndpointProfile assetEndpointProfile, Asset asset, string assetName, CancellationToken cancellationToken = default)
        {
            _assets.TryAdd(assetName, asset);

            if (asset!.Specification!.Datasets == null)
            {
                _logger.LogInformation($"Asset with name {assetName} has no datasets to sample");
            }
            else
            {
                foreach (var dataset in asset!.Specification!.Datasets)
                {
                    // This may register a message schema that has already been uploaded, but the schema registry service is idempotent
                    var datasetMessageSchema = await _messageSchemaProviderFactory.GetMessageSchemaAsync(assetEndpointProfile, asset, dataset.Name!, dataset);
                    if (datasetMessageSchema != null)
                    {
                        _logger.LogInformation($"Registering message schema for dataset with name {dataset.Name} on asset with name {assetName}");
                        await using SchemaRegistryClient schemaRegistryClient = new(_applicationContext, _mqttClient);
                        await schemaRegistryClient.PutAsync(
                            datasetMessageSchema.SchemaContent,
                            datasetMessageSchema.SchemaFormat,
                            datasetMessageSchema.SchemaType,
                            datasetMessageSchema.Version ?? "1.0.0",
                            datasetMessageSchema.Tags,
                            null,
                            cancellationToken);
                    }
                    else
                    {
                        _logger.LogInformation($"No message schema will be registered for dataset with name {dataset.Name} on asset with name {assetName}");
                    }
                }
            }

            if (asset!.Specification.Events == null)
            {
                _logger.LogInformation($"Asset with name {assetName} has no events to listen for");
            }
            else
            {
                foreach (var assetEvent in asset!.Specification.Events)
                {
                    // This may register a message schema that has already been uploaded, but the schema registry service is idempotent
                    var eventMessageSchema = await _messageSchemaProviderFactory.GetMessageSchemaAsync(assetEndpointProfile, asset, assetEvent!.Name!, assetEvent);
                    if (eventMessageSchema != null)
                    {
                        _logger.LogInformation($"Registering message schema for event with name {assetEvent.Name} on asset with name {assetName}");
                        await using SchemaRegistryClient schemaRegistryClient = new(_applicationContext, _mqttClient);
                        await schemaRegistryClient.PutAsync(
                            eventMessageSchema.SchemaContent,
                            eventMessageSchema.SchemaFormat,
                            eventMessageSchema.SchemaType,
                            eventMessageSchema.Version ?? "1.0.0",
                            eventMessageSchema.Tags,
                            null,
                            cancellationToken);
                    }
                    else
                    {
                        _logger.LogInformation($"No message schema will be registered for event with name {assetEvent.Name} on asset with name {assetName}");
                    }
                }
            }

            OnAssetAvailable?.Invoke(this, new(assetName, asset, assetEndpointProfile));
        }

        public async Task ForwardSampledDatasetAsync(Asset asset, AssetDatasetSchemaElement dataset, byte[] serializedPayload, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _logger.LogInformation($"Received sampled payload from dataset with name {dataset.Name} in asset with name {asset.Name}. Now publishing it to MQTT broker: {Encoding.UTF8.GetString(serializedPayload)}");

            Topic topic = dataset.Topic ?? asset.Specification!.DefaultTopic ?? throw new AssetConfigurationException($"Dataset with name {dataset.Name} in asset with name {asset.Name} has no configured MQTT topic to publish to. Data won't be forwarded for this dataset.");
            var mqttMessage = new MqttApplicationMessage(topic.Path!)
            {
                PayloadSegment = serializedPayload,
                Retain = topic.Retain == Retain.Keep,
            };

            MqttClientPublishResult puback = await _mqttClient.PublishAsync(mqttMessage, cancellationToken);

            if (puback.ReasonCode == MqttClientPublishReasonCode.Success
                || puback.ReasonCode == MqttClientPublishReasonCode.NoMatchingSubscribers)
            {
                // NoMatchingSubscribers case is still successful in the sense that the PUBLISH packet was delivered to the broker successfully.
                // It does suggest that the broker has no one to send that PUBLISH packet to, though.
                _logger.LogInformation($"Message was accepted by the MQTT broker with PUBACK reason code: {puback.ReasonCode} and reason {puback.ReasonString}");
            }
            else
            {
                _logger.LogInformation($"Received unsuccessful PUBACK from MQTT broker: {puback.ReasonCode} with reason {puback.ReasonString}");
            }
        }

        public async Task ForwardReceivedEventAsync(Asset asset, AssetEventSchemaElement assetEvent, byte[] serializedPayload, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _logger.LogInformation($"Received event with name {assetEvent.Name} in asset with name {asset.Name}. Now publishing it to MQTT broker.");

            Topic topic = assetEvent.Topic ?? asset.Specification!.DefaultTopic ?? throw new AssetConfigurationException($"Event with name {assetEvent.Name} in asset with name {asset.Name} has no configured MQTT topic to publish to. Data won't be forwarded for this event.");
            var mqttMessage = new MqttApplicationMessage(topic.Path!)
            {
                PayloadSegment = serializedPayload,
                Retain = topic.Retain == Retain.Keep,
            };

            MqttClientPublishResult puback = await _mqttClient.PublishAsync(mqttMessage, cancellationToken);

            if (puback.ReasonCode == MqttClientPublishReasonCode.Success
                || puback.ReasonCode == MqttClientPublishReasonCode.NoMatchingSubscribers)
            {
                // NoMatchingSubscribers case is still successful in the sense that the PUBLISH packet was delivered to the broker successfully.
                // It does suggest that the broker has no one to send that PUBLISH packet to, though.
                _logger.LogInformation($"Message was accepted by the MQTT broker with PUBACK reason code: {puback.ReasonCode} and reason {puback.ReasonString}");
            }
            else
            {
                _logger.LogInformation($"Received unsuccessful PUBACK from MQTT broker: {puback.ReasonCode} with reason {puback.ReasonString}");
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _isDisposed = true;
        }
    }
}
