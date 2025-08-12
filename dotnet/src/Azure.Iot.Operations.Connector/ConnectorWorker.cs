// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
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

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Base class for a connector worker that allows users to forward data sampled from datasets and/or data received from events.
    /// </summary>
    public class ConnectorWorker : ConnectorBackgroundService
    {
        protected readonly ILogger<ConnectorWorker> _logger;
        private readonly IMqttClient _mqttClient;
        private readonly ApplicationContext _applicationContext;
        private readonly IAdrClientWrapperProvider _adrClientWrapperFactory;
        protected IAdrClientWrapper? _adrClient;
        private readonly IMessageSchemaProvider _messageSchemaProviderFactory;
        private LeaderElectionClient? _leaderElectionClient;
        private readonly ConcurrentDictionary<string, DeviceContext> _devices = new();
        private bool _isDisposed = false;
        private readonly ConnectorLeaderElectionConfiguration? _leaderElectionConfiguration;

        // Keys are <deviceName>_<inboundEndpointName> and values are the running task and their cancellation token to signal once the device is no longer available or the connector is shutting down
        private readonly ConcurrentDictionary<string, UserTaskContext> _deviceTasks = new();

        // Keys are <deviceName>_<inboundEndpointName>_<assetName> and values are the running task and their cancellation token to signal once the asset is no longer available or the connector is shutting down
        private readonly ConcurrentDictionary<string, UserTaskContext> _assetTasks = new();

        /// <summary>
        /// Event handler for when an device becomes available.
        /// </summary>
        /// <remarks>
        /// The provided cancellation is signaled when the device is no longer available or when this connector is no longer the leader (and no longer responsible for interacting with the device).
        /// </remarks>
        public Func<DeviceAvailableEventArgs, CancellationToken, Task>? WhileDeviceIsAvailable;

        /// <summary>
        /// The function to run while an asset is available.
        /// </summary>
        /// <remarks>
        /// The provided cancellation is signaled when the asset is no longer available or when this connector is no longer the leader (and no longer responsible for interacting with the asset).
        /// </remarks>
        public Func<AssetAvailableEventArgs, CancellationToken, Task>? WhileAssetIsAvailable;

        public ConnectorWorker(
            ApplicationContext applicationContext,
            ILogger<ConnectorWorker> logger,
            IMqttClient mqttClient,
            IMessageSchemaProvider messageSchemaProviderFactory,
            IAdrClientWrapperProvider adrClientWrapperFactory,
            IConnectorLeaderElectionConfigurationProvider? leaderElectionConfigurationProvider = null)
        {
            _applicationContext = applicationContext;
            _logger = logger;
            _mqttClient = mqttClient;
            _messageSchemaProviderFactory = messageSchemaProviderFactory;
            _adrClientWrapperFactory = adrClientWrapperFactory;
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
                        await _leaderElectionClient.CampaignAsync(_leaderElectionConfiguration.LeadershipPositionTermLength, null, null, cancellationToken);

                        isLeader = true;
                        _logger.LogInformation("This pod was elected leader.");
                    }
                }

                _adrClient = _adrClientWrapperFactory.CreateAdrClientWrapper(_applicationContext, _mqttClient);

                _adrClient.DeviceChanged += OnDeviceChanged;
                _adrClient.AssetChanged += OnAssetChanged;

                _logger.LogInformation("Starting to observe devices...");
                _adrClient.ObserveDevices();

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
                        await _adrClient.UnobserveAllAsync(CancellationToken.None);
                    }
                    else if (linkedToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Connector is no longer leader. Restarting to campaign for the leadership position.");
                        await _adrClient.UnobserveAllAsync(cancellationToken);
                    }
                }

                _adrClient.DeviceChanged -= OnDeviceChanged;
                _adrClient.AssetChanged -= OnAssetChanged;

                List<Task> tasksToAwait = new();

                _logger.LogInformation("Stopping all tasks that run while an asset is available");
                foreach (UserTaskContext userTaskContext in _assetTasks.Values.ToList())
                {
                    // Cancel all tasks that run while an asset is available
                    userTaskContext.CancellationTokenSource.Cancel();
                    userTaskContext.CancellationTokenSource.Dispose();

                    tasksToAwait.Add(userTaskContext.UserTask);
                }

                _logger.LogInformation("Stopping all tasks that run while a device is available");
                foreach (UserTaskContext userTaskContext in _deviceTasks.Values.ToList())
                {
                    // Cancel all tasks that run while a device is available
                    userTaskContext.CancellationTokenSource.Cancel();
                    userTaskContext.CancellationTokenSource.Dispose();

                    tasksToAwait.Add(userTaskContext.UserTask);
                }

                _logger.LogInformation("Waiting for all user-defined tasks to complete");
                try
                {
                    await Task.WhenAll(tasksToAwait);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Encountered an error while waiting for all the user-defined tasks to complete");
                }
            }

            _logger.LogInformation("Shutting down connector...");

            _leaderElectionClient?.DisposeAsync();
            await _mqttClient.DisconnectAsync(null, CancellationToken.None);
        }

        /// <summary>
        /// Push a sampled dataset to the configured destinations.
        /// </summary>
        /// <param name="asset">The asset that the dataset belongs to.</param>
        /// <param name="dataset">The dataset that was sampled.</param>
        /// <param name="serializedPayload">The payload to push to the configured destinations.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ForwardSampledDatasetAsync(Asset asset, AssetDataset dataset, byte[] serializedPayload, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _logger.LogInformation($"Received sampled payload from dataset with name {dataset.Name} in asset with name {asset.DisplayName}. Now publishing it to MQTT broker: {Encoding.UTF8.GetString(serializedPayload)}");

            if (dataset.Destinations == null)
            {
                _logger.LogError("Cannot forward sampled dataset because it has no configured destinations");
                return;
            }

            foreach (var destination in dataset.Destinations)
            {
                if (destination.Target == DatasetTarget.Mqtt)
                {
                    string topic = destination.Configuration.Topic ?? throw new AssetConfigurationException($"Dataset with name {dataset.Name} in asset with name {asset.DisplayName} has no configured MQTT topic to publish to. Data won't be forwarded for this dataset.");
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

        /// <summary>
        /// Push a received event payload to the configured destinations.
        /// </summary>
        /// <param name="asset">The asset that this event came from.</param>
        /// <param name="assetEvent">The event.</param>
        /// <param name="serializedPayload">The payload to push to the configured destinations.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ForwardReceivedEventAsync(Asset asset, AssetEvent assetEvent, byte[] serializedPayload, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _logger.LogInformation($"Received event with name {assetEvent.Name} in asset with name {asset.DisplayName}. Now publishing it to MQTT broker.");

            if (assetEvent.Destinations == null)
            {
                _logger.LogError("Cannot forward received event because it has no configured destinations");
                return;
            }

            foreach (var destination in assetEvent.Destinations)
            {
                if (destination.Target == EventStreamTarget.Mqtt)
                {
                    string topic = destination.Configuration.Topic ?? throw new AssetConfigurationException($"Dataset with name {assetEvent.Name} in asset with name {asset.DisplayName} has no configured MQTT topic to publish to. Data won't be forwarded for this dataset.");
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

        private async void OnAssetChanged(object? _, AssetChangedEventArgs args)
        {
            string compoundDeviceName = $"{args.DeviceName}_{args.InboundEndpointName}";
            if (args.ChangeType == ChangeType.Created)
            {
                _logger.LogInformation("Asset with name {0} created on endpoint with name {1} on device with name {2}", args.AssetName, args.InboundEndpointName, args.DeviceName);
                await AssetAvailableAsync(args.DeviceName, args.InboundEndpointName, args.Asset, args.AssetName);
                _adrClient!.ObserveAssets(args.DeviceName, args.InboundEndpointName);
            }
            else if (args.ChangeType == ChangeType.Deleted)
            {
                _logger.LogInformation("Asset with name {0} deleted from endpoint with name {1} on device with name {2}", args.AssetName, args.InboundEndpointName, args.DeviceName);
                await AssetUnavailableAsync(args.DeviceName, args.InboundEndpointName, args.AssetName, false);
                await _adrClient!.UnobserveAssetsAsync(args.DeviceName, args.InboundEndpointName);
            }
            else if (args.ChangeType == ChangeType.Updated)
            {
                _logger.LogInformation("Asset with name {0} updated on endpoint with name {1} on device with name {2}", args.AssetName, args.InboundEndpointName, args.DeviceName);
                await AssetUnavailableAsync(args.DeviceName, args.InboundEndpointName, args.AssetName, true);
                await AssetAvailableAsync(args.DeviceName, args.InboundEndpointName, args.Asset, args.AssetName);
            }
        }

        private async void OnDeviceChanged(object? _, DeviceChangedEventArgs args)
        {
            string compoundDeviceName = $"{args.DeviceName}_{args.InboundEndpointName}";
            if (args.ChangeType == ChangeType.Created)
            {
                _logger.LogInformation("Device with name {0} and/or its endpoint with name {} was created", args.DeviceName, args.InboundEndpointName);
                DeviceAvailable(args, compoundDeviceName);
                if (args.Device != null)
                {
                    if (WhileDeviceIsAvailable != null)
                    {
                        CancellationTokenSource deviceTaskCancellationTokenSource = new();

                        // Do not block on this call because the user callback is designed to run for extended periods of time.
                        Task userTask = Task.Run(async () =>
                        {
                            try
                            {
                                await WhileDeviceIsAvailable.Invoke(new(args.Device, args.InboundEndpointName, _leaderElectionClient), deviceTaskCancellationTokenSource.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                // This is the expected way for the callback to exit since this layer signals the cancellation token
                            }
                        });

                        _deviceTasks.TryAdd(compoundDeviceName, new(userTask, deviceTaskCancellationTokenSource));
                    }
                }
            }
            else if (args.ChangeType == ChangeType.Deleted)
            {
                _logger.LogInformation("Device with name {0} and/or its endpoint with name {} was deleted", args.DeviceName, args.InboundEndpointName);
                await DeviceUnavailableAsync(args, compoundDeviceName, false);
                if (_deviceTasks.TryRemove(compoundDeviceName, out UserTaskContext? userTaskContext))
                {
                    userTaskContext.CancellationTokenSource.Cancel();
                    userTaskContext.CancellationTokenSource.Dispose();

                    try
                    {
                        await userTaskContext.UserTask;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Encountered an exception while cancelling user-defined task for device name {deviceName}, inbound endpoint name {inboundEndpointName}", args.DeviceName, args.InboundEndpointName);
                    }
                }
            }
            else if (args.ChangeType == ChangeType.Updated)
            {
                _logger.LogInformation("Device with name {0} and/or its endpoint with name {} was updated", args.DeviceName, args.InboundEndpointName);
                await DeviceUnavailableAsync(args, compoundDeviceName, true);
                DeviceAvailable(args, compoundDeviceName);
            }
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
                _devices[compoundDeviceName] = new(args.DeviceName, args.InboundEndpointName, args.Device);
                _adrClient!.ObserveAssets(args.DeviceName, args.InboundEndpointName);
            }
        }

        private async Task DeviceUnavailableAsync(DeviceChangedEventArgs args, string compoundDeviceName, bool isUpdating)
        {
            await _adrClient!.UnobserveAssetsAsync(args.DeviceName, args.InboundEndpointName);

            if (_devices.TryRemove(compoundDeviceName, out var deviceContext))
            {
                foreach (string assetName in deviceContext.Assets.Keys)
                {
                    await AssetUnavailableAsync(args.DeviceName, args.InboundEndpointName, assetName, isUpdating);
                }
            }
        }

        private async Task AssetAvailableAsync(string deviceName, string inboundEndpointName, Asset? asset, string assetName)
        {
            string compoundDeviceName = $"{deviceName}_{inboundEndpointName}";

            if (asset == null)
            {
                // Should never happen
                _logger.LogError("Received notification that asset was created, but no asset was provided");
                return;
            }

            if (!_devices.TryGetValue(compoundDeviceName, out DeviceContext? deviceContext))
            {
                _logger.LogWarning("Received notification of asset with name {} becoming available on device {} with inbound endpoint name {}, but that device and/or inbound endpoint are not available. Ignoring this unexpected asset", assetName, deviceName, inboundEndpointName);
                return;
            }

            deviceContext.Assets.TryAdd(assetName, asset);

            Device? device = deviceContext.Device;

            if (device == null)
            {
                _logger.LogWarning("Failed to correlate a newly available asset to its device");
                return;
            }

            if (asset.Datasets == null)
            {
                _logger.LogInformation($"Asset with name {assetName} has no datasets to sample");
            }
            else
            {
                foreach (var dataset in asset.Datasets)
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
                                datasetMessageSchema.Tags);
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

            if (asset.Events == null)
            {
                _logger.LogInformation($"Asset with name {assetName} has no events to listen for");
            }
            else
            {
                foreach (var assetEvent in asset!.Events)
                {
                    // This may register a message schema that has already been uploaded, but the schema registry service is idempotent
                    var eventMessageSchema = await _messageSchemaProviderFactory.GetMessageSchemaAsync(device, asset, assetEvent.Name, assetEvent);
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
                                eventMessageSchema.Tags);
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

            if (WhileAssetIsAvailable != null)
            {
                CancellationTokenSource assetTaskCancellationTokenSource = new();

                // Do not block on this call because the user callback is designed to run for extended periods of time.
                Task userTask = Task.Run(async () =>
                {
                    try
                    {
                        await WhileAssetIsAvailable.Invoke(new(deviceName, device, inboundEndpointName, assetName, asset, _leaderElectionClient), assetTaskCancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // This is the expected way for the callback to exit since this layer signals the cancellation token
                    }
                });

                _assetTasks.TryAdd(GetCompoundAssetName(compoundDeviceName, assetName), new(userTask, assetTaskCancellationTokenSource));
            }
        }

        private async Task AssetUnavailableAsync(string deviceName, string inboundEndpointName, string assetName, bool isUpdating)
        {
            string compoundDeviceName = $"{deviceName}_{inboundEndpointName}";

            // This method may be called either when an asset was updated or when it was deleted. If it was updated, then it will still be sampleable.
            if (!isUpdating)
            {
                if (_assetTasks.TryRemove(GetCompoundAssetName(compoundDeviceName, assetName), out UserTaskContext? userTaskContext))
                {
                    userTaskContext.CancellationTokenSource.Cancel();
                    userTaskContext.CancellationTokenSource.Dispose();

                    try
                    {
                        await userTaskContext.UserTask;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Encountered an exception while cancelling user-defined task for device name {deviceName}, inbound endpoint name {inboundEndpointName}, asset name {assetName}", deviceName, inboundEndpointName, assetName);
                    }
                }
            }
        }

        private string GetCompoundAssetName(string compoundDeviceName, string assetName)
        {
            return compoundDeviceName + "_" + assetName;
        }
    }
}
