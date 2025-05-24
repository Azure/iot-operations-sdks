// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.ConnectorConfigurations;
using Azure.Iot.Operations.Connector.Exceptions;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Azure.Iot.Operations.Services.LeaderElection;
using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Services.StateStore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Base class for a connector worker that allows users to forward data samplied from datasets and forwarding of received events.
    /// </summary>
    public class ConnectorWorker : ConnectorBackgroundService
    {
        protected readonly ILogger<ConnectorWorker> _logger;
        private readonly IMqttClient _mqttClient;
        private readonly ApplicationContext _applicationContext;
        protected readonly IAdrClientWrapper _assetMonitor;
        private readonly IMessageSchemaProvider _messageSchemaProviderFactory;
        private LeaderElectionClient? _leaderElectionClient;
        private readonly ConcurrentDictionary<string, DeviceContext> _devices = new();
        private bool _isDisposed = false;

        /// <summary>
        /// Event handler for when an device becomes available.
        /// </summary>
        public EventHandler<DeviceAvailableEventArgs>? OnDeviceAvailable;

        /// <summary>
        /// Event handler for when an device becomes unavailable.
        /// </summary>
        public EventHandler<DeviceUnavailableEventArgs>? OnDeviceUnavailable;

        /// <summary>
        /// Event handler for when an asset becomes available.
        /// </summary>
        public EventHandler<AssetAvailableEventArgs>? OnAssetAvailable;

        /// <summary>
        /// Event handler for when an asset becomes unavailable.
        /// </summary>
        public EventHandler<AssetUnavailableEventArgs>? OnAssetUnavailable;

        private readonly ConnectorLeaderElectionConfiguration? _leaderElectionConfiguration; //TODO one connector as leader for all devices? Or will some connectors have a subset of devices?

        public ConnectorWorker(
            ApplicationContext applicationContext,
            ILogger<ConnectorWorker> logger,
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
            bool readMqttConnectionSettings = false;
            MqttConnectionSettings? mqttConnectionSettings = null;
            int maxRetryCount = 10;
            int currentRetryCount = 0;
            while (!readMqttConnectionSettings)
            {
                try
                {
                    // Create MQTT client from credentials provided by the operator
                    mqttConnectionSettings = ConnectorFileMountSettings.FromFileMount();
                    readMqttConnectionSettings = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to read the file mount for MQTT connection settings. Will try again: {}", ex.Message);
                    await Task.Delay(TimeSpan.FromMilliseconds(100));

                    if (++currentRetryCount >= maxRetryCount)
                    {
                        throw;
                    }
                }
            }

            _logger.LogInformation("Connecting to MQTT broker");

            await _mqttClient.ConnectAsync(mqttConnectionSettings!, cancellationToken);

            _logger.LogInformation($"Successfully connected to MQTT broker");

            while (!cancellationToken.IsCancellationRequested)
            {
                bool isLeader = true;
                using CancellationTokenSource leadershipPositionRevokedOrUserCancelledCancellationToken
                    = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                CancellationToken linkedToken = leadershipPositionRevokedOrUserCancelledCancellationToken.Token;

                if (_leaderElectionConfiguration != null)
                {
                    isLeader = false;
                    while (!isLeader && !cancellationToken.IsCancellationRequested)
                    {
                        string leadershipPositionId = _leaderElectionConfiguration.LeadershipPositionId;

                        _logger.LogInformation($"Leadership position Id {leadershipPositionId} was configured, so this pod will perform leader election");

                        _leaderElectionClient = new(_applicationContext, _mqttClient, leadershipPositionId, mqttConnectionSettings!.ClientId)
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
                            isLeader = args.NewLeader != null && args.NewLeader.GetString().Equals(mqttConnectionSettings.ClientId);
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

                _assetMonitor.DeviceChanged += async (sender, args) =>
                {
                    string compoundDeviceName = $"{args.DeviceName}_{args.InboundEndpointName}";
                    if (args.ChangeType == ChangeType.Created)
                    {
                        _logger.LogInformation("Device with name {0} and/or its endpoint with name {} was created", args.DeviceName, args.InboundEndpointName);
                        DeviceAvailable(args, compoundDeviceName);
                        if (args.Device != null)
                        {
                            OnDeviceAvailable?.Invoke(this, new(args.Device, args.InboundEndpointName));
                        }
                    }
                    else if (args.ChangeType == ChangeType.Deleted)
                    {
                        _logger.LogInformation("Device with name {0} and/or its endpoint with name {} was deleted", args.DeviceName, args.InboundEndpointName);
                        await DeviceUnavailableAsync(args, compoundDeviceName, false);
                        OnDeviceUnavailable?.Invoke(this, new(args.DeviceName, args.InboundEndpointName));
                    }
                    else if (args.ChangeType == ChangeType.Updated)
                    {
                        _logger.LogInformation("Device with name {0} and/or its endpoint with name {} was updated", args.DeviceName, args.InboundEndpointName);
                        await DeviceUnavailableAsync(args, compoundDeviceName, true);
                        DeviceAvailable(args, compoundDeviceName);
                    }
                };

                _assetMonitor.AssetChanged += async (sender, args) =>
                {
                    string compoundDeviceName = $"{args.DeviceName}_{args.InboundEndpointName}";
                    if (args.ChangeType == ChangeType.Created)
                    {
                        _logger.LogInformation("Asset with name {0} created on endpoint with name {1} on device with name {2}", args.AssetName, args.InboundEndpointName, args.DeviceName);
                        await AssetAvailableAsync(args.DeviceName, args.InboundEndpointName, args.Asset, args.AssetName, linkedToken);
                        _assetMonitor.ObserveAssets(args.DeviceName, args.InboundEndpointName);
                    }
                    else if (args.ChangeType == ChangeType.Deleted)
                    {
                        _logger.LogInformation("Asset with name {0} deleted from endpoint with name {1} on device with name {2}", args.AssetName, args.InboundEndpointName, args.DeviceName);
                        AssetUnavailable(args.DeviceName, args.InboundEndpointName, args.AssetName, false);
                        await _assetMonitor.UnobserveAssetsAsync(args.DeviceName, args.InboundEndpointName);
                    }
                    else if (args.ChangeType == ChangeType.Updated)
                    {
                        _logger.LogInformation("Asset with name {0} updated on endpoint with name {1} on device with name {2}", args.AssetName, args.InboundEndpointName, args.DeviceName);
                        AssetUnavailable(args.DeviceName, args.InboundEndpointName, args.AssetName, true);
                        await AssetAvailableAsync(args.DeviceName, args.InboundEndpointName, args.Asset, args.AssetName, linkedToken);
                    }
                };

                _logger.LogInformation("Starting to observe devices...");
                _assetMonitor.ObserveDevices();

                try
                {
                    // Wait until the background service is cancelled or the pod is no longer leader
                    await Task.Delay(-1, linkedToken);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Connector app was cancelled. Shutting down now.");

                        // Don't propagate the user-provided cancellation token since it has already been cancelled.
                        await _assetMonitor.UnobserveAllAsync(CancellationToken.None);
                    }
                    else if (linkedToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Connector is no longer leader. Restarting to campaign for the leadership position.");
                        await _assetMonitor.UnobserveAllAsync(cancellationToken);
                    }
                }
            }

            _logger.LogInformation("Shutting down connector...");

            _leaderElectionClient?.DisposeAsync();
            await _mqttClient.DisconnectAsync(null, CancellationToken.None);
        }

        public async Task ForwardSampledDatasetAsync(Asset asset, AssetDatasetSchemaElement dataset, byte[] serializedPayload, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _logger.LogInformation($"Received sampled payload from dataset with name {dataset.Name} in asset with name {asset.Name}. Now publishing it to MQTT broker: {Encoding.UTF8.GetString(serializedPayload)}");

            if (dataset.Destinations == null)
            {
                _logger.LogError("Cannot forward sampled dataset because it has no configured destinations");
                return;
            }

            foreach (var destination in dataset.Destinations)
            {
                if (destination.Target == DatasetTarget.Mqtt)
                {
                    string topic = destination.Configuration.Topic ?? throw new AssetConfigurationException($"Dataset with name {dataset.Name} in asset with name {asset.Name} has no configured MQTT topic to publish to. Data won't be forwarded for this dataset.");
                    var mqttMessage = new MqttApplicationMessage(topic)
                    {
                        PayloadSegment = serializedPayload,
                    };

                    Retain? retain = destination.Configuration.Retain;
                    if (retain != null)
                    {
                        mqttMessage.Retain = retain == Retain.Keep;
                    }

                    ulong? ttl = destination.Configuration.Ttl;
                    if (ttl != null)
                    {
                        mqttMessage.MessageExpiryInterval = (uint)ttl.Value;
                    }

                    MqttClientPublishResult puback = await _mqttClient.PublishAsync(mqttMessage, cancellationToken);

                    if (puback.ReasonCode == MqttClientPublishReasonCode.Success
                        || puback.ReasonCode == MqttClientPublishReasonCode.NoMatchingSubscribers)
                    {
                        // NoMatchingSubscribers case is still successful in the sense that the PUBLISH packet was delivered to the broker successfully.
                        // It does suggest that the broker has no one to send that PUBLISH packet to, though.
                        _logger.LogInformation($"Message was accepted by the MQTT broker with PUBACK reason code: {puback.ReasonCode} and reason {puback.ReasonString} on topic {mqttMessage.Topic}");
                    }
                    else
                    {
                        _logger.LogInformation($"Received unsuccessful PUBACK from MQTT broker: {puback.ReasonCode} with reason {puback.ReasonString}");
                    }
                }
                else if (destination.Target == DatasetTarget.BrokerStateStore)
                {
                    await using StateStoreClient stateStoreClient = new(_applicationContext, _mqttClient);

                    string stateStoreKey = destination.Configuration.Key ?? throw new AssetConfigurationException("Cannot publish sampled dataset to state store as it has no configured key");

                    StateStoreSetResponse response = await stateStoreClient.SetAsync(stateStoreKey, new(serializedPayload));

                    if (response.Success)
                    {
                        _logger.LogInformation($"Message was accepted by the state store in key {stateStoreKey}");
                    }
                    else
                    {
                        _logger.LogError($"Message was not accepted by the state store");
                    }
                }
                else if (destination.Target == DatasetTarget.Storage)
                {
                    throw new NotImplementedException();
                }
            }
        }

        public async Task ForwardReceivedEventAsync(Asset asset, AssetEventSchemaElement assetEvent, byte[] serializedPayload, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _logger.LogInformation($"Received event with name {assetEvent.Name} in asset with name {asset.Name}. Now publishing it to MQTT broker.");

            if (assetEvent.Destinations == null)
            {
                _logger.LogError("Cannot forward received event because it has no configured destinations");
                return;
            }

            foreach (var destination in assetEvent.Destinations)
            {
                if (destination.Target == EventStreamTarget.Mqtt)
                {
                    string topic = destination.Configuration.Topic ?? throw new AssetConfigurationException($"Dataset with name {assetEvent.Name} in asset with name {asset.Name} has no configured MQTT topic to publish to. Data won't be forwarded for this dataset.");
                    var mqttMessage = new MqttApplicationMessage(topic)
                    {
                        PayloadSegment = serializedPayload,
                    };

                    Retain? retain = destination.Configuration.Retain;
                    if (retain != null)
                    {
                        mqttMessage.Retain = retain == Retain.Keep;
                    }

                    MqttClientPublishResult puback = await _mqttClient.PublishAsync(mqttMessage, cancellationToken);

                    if (puback.ReasonCode == MqttClientPublishReasonCode.Success
                        || puback.ReasonCode == MqttClientPublishReasonCode.NoMatchingSubscribers)
                    {
                        // NoMatchingSubscribers case is still successful in the sense that the PUBLISH packet was delivered to the broker successfully.
                        // It does suggest that the broker has no one to send that PUBLISH packet to, though.
                        _logger.LogInformation($"Message was accepted by the MQTT broker with PUBACK reason code: {puback.ReasonCode} and reason {puback.ReasonString} on topic {mqttMessage.Topic}");
                    }
                    else
                    {
                        _logger.LogInformation($"Received unsuccessful PUBACK from MQTT broker: {puback.ReasonCode} with reason {puback.ReasonString}");
                    }
                }
                else if (destination.Target == EventStreamTarget.Storage)
                {
                    throw new NotImplementedException();
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _isDisposed = true;
        }

        private void DeviceAvailable(DeviceChangedEventArgs args, string compoundDeviceName)
        {
            if (args.Device == null)
            {
                // shouldn't ever happen
                _logger.LogError("Received notification that device was created, but no device was provided");
            }
            else
            {
                _devices[compoundDeviceName] = new(args.DeviceName, args.InboundEndpointName)
                {
                    Device = args.Device
                };
                _assetMonitor.ObserveAssets(args.DeviceName, args.InboundEndpointName);
            }
        }

        private async Task DeviceUnavailableAsync(DeviceChangedEventArgs args, string compoundDeviceName, bool isUpdating)
        {
            await _assetMonitor.UnobserveAssetsAsync(args.DeviceName, args.InboundEndpointName);

            if (_devices.TryRemove(compoundDeviceName, out var deviceContext))
            {
                foreach (string assetName in deviceContext.Assets.Keys)
                {
                    AssetUnavailable(args.DeviceName, args.InboundEndpointName, assetName, isUpdating);
                }
            }
        }

        private async Task AssetAvailableAsync(string deviceName, string inboundEndpointName, Asset? asset, string assetName, CancellationToken cancellationToken = default)
        {
            string compoundDeviceName = $"{deviceName}_{inboundEndpointName}";

            if (asset == null)
            {
                // Should never happen
                _logger.LogError("Received notification that asset was created, but no asset was provided");
                return;
            }

            if (_devices.TryGetValue(compoundDeviceName, out DeviceContext? deviceContext))
            {
                deviceContext.Assets.TryAdd(assetName, asset);
            }

            Device? device = _devices[compoundDeviceName].Device;

            if (device == null)
            {
                _logger.LogWarning("Failed to correlate a newly available asset to its device");
                return;
            }

            if (asset!.Specification!.Datasets == null)
            {
                _logger.LogInformation($"Asset with name {assetName} has no datasets to sample");
            }
            else
            {
                foreach (var dataset in asset!.Specification!.Datasets)
                {
                    // This may register a message schema that has already been uploaded, but the schema registry service is idempotent
                    var datasetMessageSchema = await _messageSchemaProviderFactory.GetMessageSchemaAsync(device, asset, dataset.Name!, dataset);
                    if (datasetMessageSchema != null)
                    {
                        try
                        {
                            _logger.LogInformation($"Registering message schema for dataset with name {dataset.Name} on asset with name {assetName} associated with device with name {deviceName} and inbound endpoint name {inboundEndpointName}");
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
                            _logger.LogError($"Failed to register message schema for dataset with name {dataset.Name} on asset with name {assetName} associated with device with name {deviceName} and inbound endpoint name {inboundEndpointName}. Error: {ex.Message}");
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"No message schema will be registered for dataset with name {dataset.Name} on asset with name {assetName} associated with device with name {deviceName} and inbound endpoint name {inboundEndpointName}");
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
                    var eventMessageSchema = await _messageSchemaProviderFactory.GetMessageSchemaAsync(device, asset, assetEvent!.Name!, assetEvent);
                    if (eventMessageSchema != null)
                    {
                        try
                        {
                            _logger.LogInformation($"Registering message schema for event with name {assetEvent.Name} on asset with name {assetName} associated with device with name {deviceName} and inbound endpoint name {inboundEndpointName}");
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
                            _logger.LogError($"Failed to register message schema for event with name {assetEvent.Name} on asset with name {assetName} associated with device with name {deviceName} and inbound endpoint name {inboundEndpointName}. Error: {ex.Message}");
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"No message schema will be registered for event with name {assetEvent.Name} on asset with name {assetName} associated with device with name {deviceName} and inbound endpoint name {inboundEndpointName}");
                    }
                }
            }

            OnAssetAvailable?.Invoke(this, new(device, inboundEndpointName, assetName, asset));
        }

        private void AssetUnavailable(string deviceName, string inboundEndpointName, string assetName, bool isUpdating)
        {
            string compoundDeviceName = $"{deviceName}_{inboundEndpointName}";

            // This method may be called either when an asset was updated or when it was deleted. If it was updated, then it will still be sampleable.
            if (!isUpdating)
            {
                OnAssetUnavailable?.Invoke(this, new(assetName));
            }
        }
    }
}
