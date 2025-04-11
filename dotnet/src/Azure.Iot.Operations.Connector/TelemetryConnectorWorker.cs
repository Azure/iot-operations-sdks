// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Exceptions;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Azure.Iot.Operations.Services.LeaderElection;
using Azure.Iot.Operations.Services.SchemaRegistry;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Text;

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
        private readonly IAdrClientWrapper _assetMonitor;
        private readonly IMessageSchemaProvider _messageSchemaProviderFactory;
        private LeaderElectionClient? _leaderElectionClient;
        private readonly ConcurrentDictionary<string, AssetEndpointProfileContext> _assetEndpointProfiles = new();
        private bool _isDisposed = false;

        /// <summary>
        /// Event handler for when an asset becomes available.
        /// </summary>
        public EventHandler<AssetAvailabileEventArgs>? OnAssetAvailable;

        /// <summary>
        /// Event handler for when an asset becomes unavailable.
        /// </summary>
        public EventHandler<AssetUnavailableEventArgs>? OnAssetUnavailable;

        private readonly ConnectorLeaderElectionConfiguration? _leaderElectionConfiguration; //TODO one connector as leader for all AEPs? Or will some connectors have a subset of AEPs?

        public TelemetryConnectorWorker(
            ApplicationContext applicationContext,
            ILogger<TelemetryConnectorWorker> logger,
            IMqttClient mqttClient,
            IMessageSchemaProvider messageSchemaProviderFactory,
            IAdrClientWrapper assetMonitor,
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

            while (!cancellationToken.IsCancellationRequested)
            {
                bool isLeader = true;
                using CancellationTokenSource leadershipPositionRevokedOrUserCancelledCancellationToken
                    = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                if (_leaderElectionConfiguration != null)
                {
                    isLeader = false;
                    while (!isLeader && !cancellationToken.IsCancellationRequested)
                    {
                        string leadershipPositionId = _leaderElectionConfiguration.LeadershipPositionId;

                        _logger.LogInformation($"Leadership position Id {leadershipPositionId} was configured, so this pod will perform leader election");

                        _leaderElectionClient = new(_applicationContext, _mqttClient, leadershipPositionId, candidateName)
                        {
                            AutomaticRenewalOptions = new LeaderElectionAutomaticRenewalOptions()
                            {
                                AutomaticRenewal = true,
                                ElectionTerm = _leaderElectionConfiguration.LeadershipPositionTermLength,
                                RenewalPeriod = _leaderElectionConfiguration.LeadershipPositionRenewalRate
                            }
                        };

                        _leaderElectionClient.LeadershipChangeEventReceivedAsync += (sender, args) =>
                        {
                            isLeader = args.NewLeader != null && args.NewLeader.GetString().Equals(candidateName);
                            if (isLeader)
                            {
                                _logger.LogInformation("Received notification that this pod is the leader");
                            }
                            else
                            {
                                _logger.LogInformation("Received notification that this pod is not the leader");
                                leadershipPositionRevokedOrUserCancelledCancellationToken.Cancel();
                            }

                            return Task.CompletedTask;
                        };

                        _logger.LogInformation("This pod is waiting to be elected leader.");
                        // Waits until elected leader
                        await _leaderElectionClient.CampaignAsync(_leaderElectionConfiguration.LeadershipPositionTermLength, null, cancellationToken);

                        isLeader = true;
                        _logger.LogInformation("This pod was elected leader.");
                    }
                }

                _assetMonitor.AssetEndpointProfileChanged += (sender, args) =>
                {
                    if (args.ChangeType == ChangeType.Created)
                    {
                        if (args.AssetEndpointProfile == null)
                        {
                            // shouldn't ever happen
                            _logger.LogError("Received notification that asset endpoint profile was created, but no asset endpoint profile was provided");
                        }
                        else
                        {
                            _assetEndpointProfiles[args.AssetEndpointProfileName] = new();
                            _assetMonitor.ObserveAssets(args.AssetEndpointProfileName);
                        }
                    }
                    else if (args.ChangeType == ChangeType.Deleted)
                    {
                        _assetMonitor.UnobserveAssets(args.AssetEndpointProfileName);
                        _assetEndpointProfiles.Remove(args.AssetEndpointProfileName, out var _);
                    }
                    else if (args.ChangeType == ChangeType.Updated)
                    {
                    }
                };

                _assetMonitor.AssetChanged += async (sender, args) =>
                {
                    string aepName = "todo"; // Should be provided by service + asset monitor, right?
                    if (!_assetEndpointProfiles.TryGetValue(aepName, out AssetEndpointProfileContext? aepContext))
                    {
                        _logger.LogError("Could not correlate asset endpoint profile name {0} with any saved asset endpoint profile", aepName);
                        return;
                    }

                    AssetEndpointProfile aep = aepContext.AssetEndpointProfile;

                    if (args.ChangeType == ChangeType.Created)
                    {
                        if (args.Asset == null)
                        {
                            // shouldn't ever happen
                            _logger.LogError("Received notification that asset was created, but no asset was provided");
                        }
                        else if (aepName != null) //TODO else?
                        {
                            await AssetAvailableAsync(aep, args.Asset, args.AssetName, leadershipPositionRevokedOrUserCancelledCancellationToken.Token);
                            _assetMonitor.ObserveAssets(aepName);
                        }
                    }
                    else if (args.ChangeType == ChangeType.Deleted)
                    {
                        AssetUnavailable(aepName, args.AssetName, false);
                        _assetMonitor.UnobserveAssets(aepName);
                    }
                    else if (args.ChangeType == ChangeType.Updated)
                    {
                        AssetUnavailable(aepName, args.AssetName, true);
                        await AssetAvailableAsync(aep, args.Asset, args.AssetName, leadershipPositionRevokedOrUserCancelledCancellationToken.Token);
                    }
                };

                _assetMonitor.ObserveAssetEndpointProfiles();

                try
                {
                    await Task.Delay(TimeSpan.MaxValue, leadershipPositionRevokedOrUserCancelledCancellationToken.Token);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Connector app was cancelled. Shutting down now.");

                        // Don't propagate the user-provided cancellation token since it has already been cancelled.
                        await _assetMonitor.UnobserveAllAsync(CancellationToken.None);
                        await _mqttClient.DisconnectAsync(null, CancellationToken.None);
                    }
                    else if (leadershipPositionRevokedOrUserCancelledCancellationToken.Token.IsCancellationRequested)
                    {
                        _logger.LogInformation("Connector is no longer leader. Restarting to campaign for the leadership position.");
                        // Don't propagate the user-provided cancellation token since 
                        await _assetMonitor.UnobserveAllAsync(cancellationToken);
                        await _mqttClient.DisconnectAsync(null, cancellationToken);
                    }
                }
            }

            _leaderElectionClient?.DisposeAsync();
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

        private async Task AssetAvailableAsync(AssetEndpointProfile assetEndpointProfile, Asset asset, string assetName, CancellationToken cancellationToken = default)
        {
            _assetEndpointProfiles[assetEndpointProfile.Name].Assets.Add(assetName, asset);

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
                        try
                        {
                            _logger.LogInformation($"Registering message schema for dataset with name {dataset.Name} on asset with name {assetName} associated with asset endpoint profile with name {assetEndpointProfile.Name}");
                            await using SchemaRegistryClient schemaRegistryClient = new(_applicationContext, _mqttClient);
                            await schemaRegistryClient.PutAsync(
                                datasetMessageSchema.SchemaContent,
                                datasetMessageSchema.SchemaFormat,
                                datasetMessageSchema.SchemaType,
                                datasetMessageSchema.Version ?? "1",
                                datasetMessageSchema.Tags,
                                null,
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Failed to register message schema for dataset with name {dataset.Name} on asset with name {assetName} associated with asset endpoint profile with name {assetEndpointProfile.Name}. Error: {ex.Message}");
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"No message schema will be registered for dataset with name {dataset.Name} on asset with name {assetName} associated with asset endpoint profile with name {assetEndpointProfile.Name}");
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
                        try
                        {
                            _logger.LogInformation($"Registering message schema for event with name {assetEvent.Name} on asset with name {assetName} associated with asset endpoint profile with name {assetEndpointProfile.Name}");
                            await using SchemaRegistryClient schemaRegistryClient = new(_applicationContext, _mqttClient);
                            await schemaRegistryClient.PutAsync(
                                eventMessageSchema.SchemaContent,
                                eventMessageSchema.SchemaFormat,
                                eventMessageSchema.SchemaType,
                                eventMessageSchema.Version ?? "1",
                                eventMessageSchema.Tags,
                                null,
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Failed to register message schema for event with name {assetEvent.Name} on asset with name {assetName} associated with asset endpoint profile with name {assetEndpointProfile.Name}. Error: {ex.Message}");
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"No message schema will be registered for event with name {assetEvent.Name} on asset with name {assetName} associated with asset endpoint profile with name {assetEndpointProfile.Name}");
                    }
                }
            }

            OnAssetAvailable?.Invoke(this, new(assetName, asset, assetEndpointProfile));
        }

        private void AssetUnavailable(string aepName, string assetName, bool isUpdating)
        {
            _assetEndpointProfiles[aepName].Assets.Remove(assetName);

            // This method may be called either when an asset was updated or when it was deleted. If it was updated, then it will still be sampleable.
            if (!isUpdating)
            {
                OnAssetUnavailable?.Invoke(this, new(assetName));
            }
        }
    }
}
