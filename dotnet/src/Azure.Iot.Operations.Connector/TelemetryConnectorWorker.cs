// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Exceptions;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
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
        private readonly IAssetMonitor _assetMonitor;
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
            IAssetMonitor assetMonitor,
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
            bool isLeader = false;

            // Create MQTT client from credentials provided by the operator
            MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();
            _logger.LogInformation($"Connecting to MQTT broker with {mqttConnectionSettings}");

            await _mqttClient.ConnectAsync(mqttConnectionSettings, cancellationToken);

            _logger.LogInformation($"Successfully connected to MQTT broker");

            bool doingLeaderElection = false;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        TaskCompletionSource aepDeletedOrUpdatedTcs = new();
                        TaskCompletionSource<AssetEndpointProfile> aepCreatedTcs = new();
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
                                    aepCreatedTcs.TrySetResult(args.AssetEndpointProfile);
                                }
                            }
                            else
                            {
                                aepDeletedOrUpdatedTcs.TrySetResult();
                            }
                        };

                        _assetMonitor.ObserveAssetEndpointProfile(null, cancellationToken);

                        _logger.LogInformation("Waiting for asset endpoint profile to be discovered");
                        AssetEndpointProfile = await aepCreatedTcs.Task.WaitAsync(cancellationToken);

                        _logger.LogInformation("Successfully discovered the asset endpoint profile");

                        if (_leaderElectionConfiguration != null)
                        {
                            doingLeaderElection = true;
                            string leadershipPositionId = _leaderElectionConfiguration.LeadershipPositionId;

                            _logger.LogInformation($"Leadership position Id {leadershipPositionId} was configured, so this pod will perform leader election");

                            await using LeaderElectionClient leaderElectionClient = new(_applicationContext, _mqttClient, leadershipPositionId, candidateName);

                            leaderElectionClient.AutomaticRenewalOptions = new LeaderElectionAutomaticRenewalOptions()
                            {
                                AutomaticRenewal = true,
                                ElectionTerm = _leaderElectionConfiguration.LeadershipPositionTermLength,
                                RenewalPeriod = _leaderElectionConfiguration.LeadershipPositionRenewalRate
                            };

                            leaderElectionClient.LeadershipChangeEventReceivedAsync += (sender, args) =>
                            {
                                isLeader = args.NewLeader != null && args.NewLeader.GetString().Equals(candidateName);
                                if (isLeader)
                                {
                                    _logger.LogInformation("Received notification that this pod is the leader");
                                }

                                return Task.CompletedTask;
                            };

                            _logger.LogInformation("This pod is waiting to be elected leader.");
                            await leaderElectionClient.CampaignAsync(_leaderElectionConfiguration.LeadershipPositionTermLength);

                            _logger.LogInformation("This pod was elected leader.");
                        }

                        _assetMonitor.AssetChanged += (sender, args) =>
                        {
                            _logger.LogInformation($"Received a notification an asset with name {args.AssetName} has been {args.ChangeType.ToString().ToLower()}.");

                            if (args.ChangeType == ChangeType.Deleted)
                            {
                                AssetUnavailable(args.AssetName, false);
                            }
                            else if (args.ChangeType == ChangeType.Created)
                            {
                                _ = AssetAvailableAsync(AssetEndpointProfile, args.Asset!, args.AssetName, cancellationToken);
                            }
                            else
                            {
                                // asset changes don't all necessitate re-creating the relevant dataset samplers, but there is no way to know
                                // at this level what changes are dataset-specific nor which of those changes require a new sampler. Because
                                // of that, this sample just assumes all asset changes require the factory requesting a new sampler.
                                AssetUnavailable(args.AssetName, true);
                                _ = AssetAvailableAsync(AssetEndpointProfile, args.Asset!, args.AssetName, cancellationToken);
                            }
                        };

                        _logger.LogInformation("Now monitoring for asset creation/deletion/updates");
                        _assetMonitor.ObserveAssets(null, cancellationToken);

                        // Wait until the worker is cancelled or it is no longer the leader
                        while (!cancellationToken.IsCancellationRequested && (isLeader || !doingLeaderElection) && !aepDeletedOrUpdatedTcs.Task.IsCompleted)
                        {
                            try
                            {
                                if (doingLeaderElection)
                                {
                                    await Task.WhenAny(
                                        aepDeletedOrUpdatedTcs.Task,
                                        Task.Delay(_leaderElectionConfiguration!.LeadershipPositionTermLength)).WaitAsync(cancellationToken);
                                }
                                else
                                {
                                    await Task.WhenAny(
                                        aepDeletedOrUpdatedTcs.Task).WaitAsync(cancellationToken);
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                // expected outcome, allow the while loop to check status again
                            }
                        }

                        if (cancellationToken.IsCancellationRequested)
                        {
                            _logger.LogInformation("This pod is shutting down. It will now stop monitoring and sampling assets.");
                        }
                        else if (aepDeletedOrUpdatedTcs.Task.IsCompleted)
                        {
                            _logger.LogInformation("Received a notification that the asset endpoint profile has changed. This pod will now cancel current asset sampling and restart monitoring assets.");
                        }
                        else if (doingLeaderElection)
                        {
                            _logger.LogInformation("This pod is no longer the leader. It will now stop monitoring and sampling assets.");
                        }
                        else
                        {
                            // Shouldn't happen. The pod should either be cancelled, the AEP should have changed, or this pod should have lost its position as leader
                            _logger.LogInformation("This pod will now cancel current asset sampling and restart monitoring assets.");
                        }

                        _assetMonitor.UnobserveAssets();
                        _assetMonitor.UnobserveAssetEndpointProfile();

                        foreach (string assetName in _assets.Keys)
                        {
                            AssetUnavailable(assetName, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Encountered an error: {ex}");
                    }
                }
            }
            finally
            {
                _logger.LogInformation("Shutting down the connector");
            }
        }

        private void AssetUnavailable(string assetName, bool isRestarting)
        {
            _assets.Remove(assetName, out Asset? _);

            // This method may be called either when an asset was updated or when it was deleted. If it was updated, then it will still be sampleable.
            if (!isRestarting)
            {
                OnAssetUnavailable?.Invoke(this, new(assetName));
            }
        }

        private async Task AssetAvailableAsync(AssetEndpointProfile assetEndpointProfile, Asset asset, string assetName, CancellationToken cancellationToken = default)
        {
            _assets.TryAdd(assetName, asset);

            if (asset.DatasetsDictionary == null)
            {
                _logger.LogInformation($"Asset with name {assetName} has no datasets to sample");
            }
            else
            {
                foreach (string datasetName in asset.DatasetsDictionary!.Keys)
                {
                    Dataset dataset = asset.DatasetsDictionary![datasetName];

                    // This may register a message schema that has already been uploaded, but the schema registry service is idempotent
                    var datasetMessageSchema = await _messageSchemaProviderFactory.GetMessageSchemaAsync(assetEndpointProfile, asset, datasetName, dataset);
                    if (datasetMessageSchema != null)
                    {
                        _logger.LogInformation($"Registering message schema for dataset with name {datasetName} on asset with name {assetName}");
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
                        _logger.LogInformation($"No message schema will be registered for dataset with name {datasetName} on asset with name {assetName}");
                    }
                }
            }

            if (asset.EventsDictionary == null)
            {
                _logger.LogInformation($"Asset with name {assetName} has no events to listen for");
            }
            else
            {
                foreach (string eventName in asset.EventsDictionary!.Keys)
                {
                    Event assetEvent = asset.EventsDictionary[eventName];

                    // This may register a message schema that has already been uploaded, but the schema registry service is idempotent
                    var eventMessageSchema = await _messageSchemaProviderFactory.GetMessageSchemaAsync(assetEndpointProfile, asset, eventName, assetEvent);
                    if (eventMessageSchema != null)
                    {
                        _logger.LogInformation($"Registering message schema for event with name {eventName} on asset with name {assetName}");
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
                        _logger.LogInformation($"No message schema will be registered for event with name {eventName} on asset with name {assetName}");
                    }
                }
            }

            OnAssetAvailable?.Invoke(this, new(assetName, asset, assetEndpointProfile));
        }

        public async Task ForwardSampledDatasetAsync(Asset asset, Dataset dataset, byte[] serializedPayload, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _logger.LogInformation($"Received sampled payload from dataset with name {dataset.Name} in asset with name {asset.DisplayName}. Now publishing it to MQTT broker: {Encoding.UTF8.GetString(serializedPayload)}");

            Topic topic = dataset.Topic ?? asset.DefaultTopic ?? throw new AssetConfigurationException($"Dataset with name {dataset.Name} in asset with name {asset.DisplayName} has no configured MQTT topic to publish to. Data won't be forwarded for this dataset.");
            var mqttMessage = new MqttApplicationMessage(topic.Path)
            {
                PayloadSegment = serializedPayload,
                Retain = topic.Retain == RetainHandling.Keep,
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

        public async Task ForwardReceivedEventAsync(Asset asset, Event assetEvent, byte[] serializedPayload, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _logger.LogInformation($"Received event with name {assetEvent.Name} in asset with name {asset.DisplayName}. Now publishing it to MQTT broker: {Encoding.UTF8.GetString(serializedPayload)}");

            Topic topic = assetEvent.Topic ?? asset.DefaultTopic ?? throw new AssetConfigurationException($"Event with name {assetEvent.Name} in asset with name {asset.DisplayName} has no configured MQTT topic to publish to. Data won't be forwarded for this event.");
            var mqttMessage = new MqttApplicationMessage(topic.Path)
            {
                PayloadSegment = serializedPayload,
                Retain = topic.Retain == RetainHandling.Keep,
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
