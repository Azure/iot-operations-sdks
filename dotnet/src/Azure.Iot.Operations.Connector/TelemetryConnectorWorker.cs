// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.Assets;
using Azure.Iot.Operations.Services.LeaderElection;
using Azure.Iot.Operations.Services.SchemaRegistry;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Base class for a worker that samples datasets from assets and publishes the data to an MQTT broker. This worker allows implementations
    /// to choose when to sample each dataset and notifies the implementation when an asset is/is not available to be sampled.
    /// </summary>
    public class TelemetryConnectorWorker : ConnectorBackgroundService
    {
        protected readonly ILogger<TelemetryConnectorWorker> _logger;
        private IMqttClient _mqttClient;
        private IAssetMonitor _assetMonitor;
        private IDatasetMessageSchemaProviderFactory _messageSchemaProviderFactory;
        private ConcurrentDictionary<string, ConcurrentDictionary<string, DatasetMessageSchema>> _assetsDatasetMessageSchemas = new();
        private ConcurrentDictionary<string, Asset> _assets = new();

        public EventHandler<AssetAvailabileEventArgs>? OnAssetAvailable;
        public EventHandler<AssetUnavailabileEventArgs>? OnAssetUnavailable;

        public AssetEndpointProfile? AssetEndpointProfile {  get; set; }

        public TelemetryConnectorWorker(
            ILogger<TelemetryConnectorWorker> logger,
            IMqttClient mqttClient,
            IDatasetMessageSchemaProviderFactory messageSchemaProviderFactory,
            IAssetMonitor assetMonitor)
        {
            _logger = logger;
            _mqttClient = mqttClient;
            _messageSchemaProviderFactory = messageSchemaProviderFactory;
            _assetMonitor = assetMonitor;
        }

        public override Task RunConnectorAsync(CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            string candidateName = Guid.NewGuid().ToString();
            bool isLeader = false;

            // Create MQTT client from credentials provided by the operator
            MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();
            mqttConnectionSettings.ClientId = Guid.NewGuid().ToString();
            _logger.LogInformation($"Connecting to MQTT broker with {mqttConnectionSettings}");

            await _mqttClient.ConnectAsync(mqttConnectionSettings, cancellationToken);

            _logger.LogInformation($"Successfully connected to MQTT broker");

            bool doingLeaderElection = false;
            TimeSpan leaderElectionTermLength = TimeSpan.FromSeconds(5);
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

                        if (AssetEndpointProfile.AdditionalConfiguration != null
                            && AssetEndpointProfile.AdditionalConfiguration.RootElement.TryGetProperty("leadershipPositionId", out JsonElement value)
                            && value.ValueKind == JsonValueKind.String
                            && value.GetString() != null)
                        {
                            doingLeaderElection = true;
                            string leadershipPositionId = value.GetString()!;

                            _logger.LogInformation($"Leadership position Id {leadershipPositionId} was configured, so this pod will perform leader election");

                            await using LeaderElectionClient leaderElectionClient = new(_mqttClient, leadershipPositionId, candidateName);

                            leaderElectionClient.AutomaticRenewalOptions = new LeaderElectionAutomaticRenewalOptions()
                            {
                                AutomaticRenewal = true,
                                ElectionTerm = leaderElectionTermLength,
                                RenewalPeriod = leaderElectionTermLength.Subtract(TimeSpan.FromSeconds(1))
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
                            await leaderElectionClient.CampaignAsync(leaderElectionTermLength);

                            _logger.LogInformation("This pod was elected leader.");
                        }

                        _assetMonitor.AssetChanged += (sender, args) =>
                        {
                            _logger.LogInformation($"Recieved a notification an asset with name {args.AssetName} has been {args.ChangeType.ToString().ToLower()}.");

                            if (args.ChangeType == ChangeType.Deleted)
                            {
                                StopSamplingAsset(args.AssetName, false, cancellationToken);
                            }
                            else if (args.ChangeType == ChangeType.Created)
                            {
                                _ = StartSamplingAssetAsync(AssetEndpointProfile, args.Asset!, args.AssetName, cancellationToken);
                            }
                            else
                            {
                                // asset changes don't all necessitate re-creating the relevant dataset samplers, but there is no way to know
                                // at this level what changes are dataset-specific nor which of those changes require a new sampler. Because
                                // of that, this sample just assumes all asset changes require the factory requesting a new sampler.
                                StopSamplingAsset(args.AssetName, true, cancellationToken);
                                _ = StartSamplingAssetAsync(AssetEndpointProfile, args.Asset!, args.AssetName, cancellationToken);
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
                                        Task.Delay(leaderElectionTermLength)).WaitAsync(cancellationToken);
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

                        // Dispose of all samplers and timers
                        foreach (string assetName in _assetsDatasetMessageSchemas.Keys)
                        {
                            StopSamplingAsset(assetName, false, cancellationToken);
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

        private void StopSamplingAsset(string assetName, bool isRestarting, CancellationToken cancellationToken)
        {
            _assetsDatasetMessageSchemas.Remove(assetName, out var _);
            _assets.Remove(assetName, out Asset? _);

            // This method may be called either when an asset was updated or when it was deleted. If it was updated, then it will still be sampleable.
            if (!isRestarting)
            {
                OnAssetUnavailable?.Invoke(this, new(assetName));
            }
        }

        private async Task StartSamplingAssetAsync(AssetEndpointProfile assetEndpointProfile, Asset asset, string assetName, CancellationToken cancellationToken = default)
        {
            if (asset.DatasetsDictionary == null)
            {
                _logger.LogInformation($"Asset with name {assetName} has no datasets to sample");
                return;
            }
            // This won't overwrite the existing asset dataset samplers if they already exist
            _assets.TryAdd(assetName, asset);
            _assetsDatasetMessageSchemas.TryAdd(assetName, new());

            foreach (string datasetName in asset.DatasetsDictionary!.Keys)
            {
                Dataset dataset = asset.DatasetsDictionary![datasetName];

                if (_assetsDatasetMessageSchemas.TryGetValue(assetName, out var assetDatasetSamplers))
                {
                    // Overwrite any previous dataset sampler for this dataset
                    _ = assetDatasetSamplers.Remove(datasetName, out var oldDatasetSampler);

                    // Create a new dataset sampler since the old one may have been updated in some way
                    var datasetMessageSchemaProvider = _messageSchemaProviderFactory.CreateDatasetMessageSchemaProvider(assetEndpointProfile, asset, dataset);
                    var datasetMessageSchema = await datasetMessageSchemaProvider.GetMessageSchemaAsync();
                    if (datasetMessageSchema != null)
                    {
                        _assetsDatasetMessageSchemas[assetName].TryAdd(datasetName, datasetMessageSchema);

                        _logger.LogInformation($"Registering message schema for dataset with name {datasetName} on asset with name {assetName}");
                        await using SchemaRegistryClient schemaRegistryClient = new(_mqttClient);
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

            OnAssetAvailable?.Invoke(this, new(assetName, asset));
        }

        public async Task ForwardSampledDatasetAsync(string assetName, string datasetName, byte[] serializedPayload, CancellationToken cancellationToken = default)
        {
            if (!_assets.TryGetValue(assetName, out Asset? asset))
            {
                return; //TODO
            }

            Dictionary<string, Dataset>? assetDatasets = asset.DatasetsDictionary;
            if (assetDatasets == null || !assetDatasets.TryGetValue(datasetName, out Dataset? dataset))
            {
                return; //TODO
            }

            _logger.LogInformation($"Received sampled payload from dataset with name {dataset.Name} in asset with name {assetName}. Now publishing it to MQTT broker: {Encoding.UTF8.GetString(serializedPayload)}");

            Topic topic;
            if (dataset.Topic != null)
            {
                topic = dataset.Topic;
            }
            else if (asset.DefaultTopic != null)
            {
                topic = asset.DefaultTopic;
            }
            else
            {
                _logger.LogError($"Dataset with name {dataset.Name} in asset with name {assetName} has no configured MQTT topic to publish to. Data won't be forwarded for this dataset.");
                return;
            }

            var mqttMessage = new MqttApplicationMessage(topic.Path)
            {
                PayloadSegment = serializedPayload,
                Retain = topic.Retain == RetainHandling.Keep,
            };

            //TODO error handling?
            MqttClientPublishResult puback = await _mqttClient.PublishAsync(mqttMessage);

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

        public async Task ForwardReceivedEventAsync(string assetName, string eventName, byte[] serializedPayload, CancellationToken cancellationToken = default)
        {
            if (!_assets.TryGetValue(assetName, out Asset? asset))
            {
                return; //TODO
            }

            Dictionary<string, Event>? assetEvents = asset.EventsDictionary;
            if (assetEvents == null || !assetEvents.TryGetValue(eventName, out Event? assetEvent))
            {
                return; //TODO
            }

            _logger.LogInformation($"Received event with name {assetEvent.Name} in asset with name {assetName}. Now publishing it to MQTT broker: {Encoding.UTF8.GetString(serializedPayload)}");

            Topic topic;
            if (assetEvent.Topic != null)
            {
                topic = assetEvent.Topic;
            }
            else if (asset.DefaultTopic != null)
            {
                topic = asset.DefaultTopic;
            }
            else
            {
                _logger.LogError($"Event with name {assetEvent.Name} in asset with name {assetName} has no configured MQTT topic to publish to. Data won't be forwarded for this event.");
                return;
            }

            var mqttMessage = new MqttApplicationMessage(topic.Path)
            {
                PayloadSegment = serializedPayload,
                Retain = topic.Retain == RetainHandling.Keep,
            };

            //TODO error handling?
            MqttClientPublishResult puback = await _mqttClient.PublishAsync(mqttMessage);

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
    }
}