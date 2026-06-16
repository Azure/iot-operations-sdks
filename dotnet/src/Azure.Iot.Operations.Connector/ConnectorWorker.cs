// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text;
using Azure.Iot.Operations.Connector.CloudEvents;
using Azure.Iot.Operations.Connector.ConnectorConfigurations;
using Azure.Iot.Operations.Connector.Exceptions;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Azure.Iot.Operations.Services.LeaderElection;
using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry;
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
        private readonly IAzureDeviceRegistryClientWrapperProvider _adrClientWrapperFactory;
        protected IAzureDeviceRegistryClientWrapper? _adrClient;
        private readonly IMessageSchemaProvider _messageSchemaProviderFactory;
        private LeaderElectionClient? _leaderElectionClient;
        private readonly ConcurrentDictionary<string, DeviceContext> _devices = new();

        private bool _isDisposed = false;
        private readonly ConnectorLeaderElectionConfiguration? _leaderElectionConfiguration;
        private readonly ConcurrentDictionary<string, ConnectorTelemetrySender> _telemetrySenderCache = new();

        // Keys are <deviceName>_<inboundEndpointName> and values are the running task and their cancellation token to signal once the device is no longer available or the connector is shutting down
        private readonly ConcurrentDictionary<string, UserTaskContext> _deviceTasks = new();

        // Keys are <deviceName>_<inboundEndpointName>_<assetName>. Long-lived per-asset runtime state:
        // the AssetClient (preserved across asset Updated events), the management-action branch task
        // (cancelled only on Deleted), and the user-callback branch (cancelled+rebuilt on every Updated).
        private readonly ConcurrentDictionary<string, AssetRuntimeContext> _assetTasks = new();

        // Per-key sequential notification chains. Created/Deleted (file monitor) and Updated (ADR MQTT
        // callback) arrive from independent sources; appending each notification after the previous one
        // for the same key processes them strictly one-at-a-time in arrival order. This is what guarantees
        // exactly one AssetClient exists per asset at any time. The locks guard only the brief append.
        private readonly Dictionary<string, Task> _assetNotificationChains = new();
        private readonly object _assetNotificationChainLock = new();
        private readonly Dictionary<string, Task> _deviceNotificationChains = new();
        private readonly object _deviceNotificationChainLock = new();

        // keys are "{composite device name}_{asset name}_{dataset name}. The value is the message schema registered for that device's asset's dataset
        private readonly ConcurrentDictionary<string, Schema> _registeredDatasetMessageSchemas = new();

        // keys are "{composite device name}_{asset name}_{event group name}_{event name}. The value is the message schema registered for that device's asset's event
        private readonly ConcurrentDictionary<string, Schema> _registeredEventMessageSchemas = new();

        /// <summary>
        /// Event handler for when a device becomes available.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The provided cancellation is signaled when the device is no longer available or when this connector is no longer the leader (and no longer responsible for interacting with the device).
        /// </para>
        /// <para>
        /// Best Practice: Check the <see cref="Device.Enabled"/> property in your handler. A disabled device should not be processed.
        /// Devices can be disabled at discovery time or while the connector is working.
        /// </para>
        /// </remarks>
        public Func<DeviceAvailableEventArgs, CancellationToken, Task>? WhileDeviceIsAvailable;

        /// <summary>
        /// The function to run while an asset is available.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The provided cancellation is signaled when the asset is no longer available or when this connector is no longer the leader (and no longer responsible for interacting with the asset).
        /// </para>
        /// <para>
        /// Best Practice: Check both <see cref="Device.Enabled"/> and <see cref="Asset.Enabled"/> properties in your handler.
        /// A disabled device or asset should not be processed. Resources can be disabled at discovery time or while the connector is working.
        /// </para>
        /// </remarks>
        public Func<AssetAvailableEventArgs, CancellationToken, Task>? WhileAssetIsAvailable;

        private readonly ManagementActionOrchestrator? _managementActionOrchestrator;

        /// <summary>
        /// Shared <see cref="ApplicationContext"/> exposed to internal SDK collaborators that build
        /// protocol clients on top of the worker's MQTT session.
        /// </summary>
        internal ApplicationContext ApplicationContext { get; }

        /// <summary>
        /// Shared MQTT pub/sub client exposed to internal SDK collaborators that build executors/senders
        /// on top of the worker's MQTT session.
        /// </summary>
        internal IMqttPubSubClient MqttPubSubClient => _mqttClient;

        public ConnectorWorker(
            ApplicationContext applicationContext,
            ILogger<ConnectorWorker> logger,
            IMqttClient mqttClient,
            IMessageSchemaProvider messageSchemaProviderFactory,
            IAzureDeviceRegistryClientWrapperProvider adrClientWrapperFactory,
            IConnectorLeaderElectionConfigurationProvider? leaderElectionConfigurationProvider = null,
            IManagementActionHandlerFactory? actionHandlerFactory = null)
        {
            ApplicationContext = applicationContext;
            _logger = logger;
            _mqttClient = mqttClient;
            _messageSchemaProviderFactory = messageSchemaProviderFactory;
            _adrClientWrapperFactory = adrClientWrapperFactory;
            _leaderElectionConfiguration = leaderElectionConfigurationProvider?.GetLeaderElectionConfiguration();
            if (actionHandlerFactory != null)
            {
                _managementActionOrchestrator = new ManagementActionOrchestrator(actionHandlerFactory, _logger);
            }
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
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

                    if (++currentRetryCount >= maxRetryCount)
                    {
                        throw;
                    }
                }
            }

            _logger.LogInformation($"Connecting to MQTT broker with client id {mqttConnectionSettings!.ClientId}");

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

                        _leaderElectionClient = new(ApplicationContext, _mqttClient, leadershipPositionId, mqttConnectionSettings!.ClientId)
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

                _adrClient = _adrClientWrapperFactory.CreateAdrClientWrapper(ApplicationContext, _mqttClient);

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
                foreach (AssetRuntimeContext assetCtx in _assetTasks.Values.ToList())
                {
                    // Cancel both branches and capture both tasks. AssetClient itself is disposed
                    // explicitly when its key is removed (AssetUnavailable on Deleted) or below in
                    // the post-shutdown cleanup; here we only want to stop the running tasks.
                    try { assetCtx.UserCts.Cancel(); } catch (ObjectDisposedException) { }
                    try { assetCtx.ManagementActionCts.Cancel(); } catch (ObjectDisposedException) { }

                    if (assetCtx.UserTask is not null)
                    {
                        tasksToAwait.Add(assetCtx.UserTask);
                    }
                    if (assetCtx.ManagementActionTask is not null)
                    {
                        tasksToAwait.Add(assetCtx.ManagementActionTask);
                    }
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

                // Dispose per-asset runtime contexts: drains the owned AssetClient (which stops/
                // disposes any cached ManagementActionExecutors) and the long-lived owned args.
                foreach (var kvp in _assetTasks.ToList())
                {
                    if (_assetTasks.TryRemove(kvp.Key, out AssetRuntimeContext? ctx))
                    {
                        try { await ctx.DisposeAsync(); }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Encountered an error while disposing asset runtime context for {Key}", kvp.Key);
                        }
                    }
                }
            }

            _logger.LogInformation("Shutting down connector...");

            _leaderElectionClient?.DisposeAsync();
            await _mqttClient.DisconnectAsync(null, CancellationToken.None);
        }

        public MessageSchemaReference? GetRegisteredDatasetMessageSchema(string deviceName, string inboundEndpointName, string assetName, string datasetName)
        {
            if (_registeredDatasetMessageSchemas.TryGetValue($"{deviceName}_{inboundEndpointName}_{assetName}_{datasetName}", out Schema? schema))
            {
                return new MessageSchemaReference()
                {
                    SchemaName = schema.Name,
                    SchemaRegistryNamespace = schema.Namespace,
                    SchemaVersion = schema.Version,
                };
            }

            return null;
        }

        public MessageSchemaReference? GetRegisteredEventMessageSchema(string deviceName, string inboundEndpointName, string assetName, string eventGroupName, string eventName)
        {
            if (_registeredEventMessageSchemas.TryGetValue($"{deviceName}_{inboundEndpointName}_{assetName}_{eventGroupName}_{eventName}", out Schema? schema))
            {
                return new MessageSchemaReference()
                {
                    SchemaName = schema.Name,
                    SchemaRegistryNamespace = schema.Namespace,
                    SchemaVersion = schema.Version,
                };
            }

            return null;
        }

        // Called by AssetClient instances
        internal async Task ForwardSampledDatasetAsync(string deviceName, Device device, string inboundEndpointName, string assetName, Asset asset,
            AssetDataset dataset, byte[] serializedPayload, Dictionary<string, string>? userData = null, string? protocolSpecificIdentifier = null,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            CloudEvents.AioCloudEvent? aioCloudEvent = null;
            Schema? registeredDatasetMessageSchema = null;
            if (!_registeredDatasetMessageSchemas.ContainsKey($"{deviceName}_{inboundEndpointName}_{assetName}_{dataset.Name}"))
            {
                // This may register a message schema that has already been uploaded, but the schema registry service is idempotent
                var datasetMessageSchema = await _messageSchemaProviderFactory.GetMessageSchemaAsync(device, asset, dataset.Name!, dataset, cancellationToken);
                if (datasetMessageSchema != null)
                {
                    try
                    {
                        _logger.LogInformation($"Registering message schema for dataset with name {dataset.Name} on asset with name {assetName} associated with device with name {deviceName} and inbound endpoint name {inboundEndpointName}");
                        await using SchemaRegistryClient schemaRegistryClient = new(ApplicationContext, _mqttClient);
                        registeredDatasetMessageSchema = await schemaRegistryClient.PutAsync(
                            datasetMessageSchema.SchemaContent,
                            datasetMessageSchema.SchemaFormat,
                            datasetMessageSchema.SchemaType,
                            datasetMessageSchema.Version ?? "1",
                            datasetMessageSchema.Tags,
                            cancellationToken: cancellationToken);

                        _logger.LogInformation($"Registered message schema for dataset with name {dataset.Name} on asset with name {assetName} associated with device with name {deviceName} and inbound endpoint name {inboundEndpointName}.");

                        _registeredDatasetMessageSchemas.TryAdd($"{deviceName}_{inboundEndpointName}_{assetName}_{dataset.Name}", registeredDatasetMessageSchema);

                        await schemaRegistryClient.StopAsync(cancellationToken);
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

            if (_registeredDatasetMessageSchemas.TryGetValue($"{deviceName}_{inboundEndpointName}_{assetName}_{dataset.Name}", out registeredDatasetMessageSchema))
            {
                aioCloudEvent = ConstructCloudEventHeadersForDataset(
                    device,
                    deviceName,
                    inboundEndpointName,
                    asset,
                    assetName,
                    dataset,
                    registeredDatasetMessageSchema,
                    protocolSpecificIdentifier);
            }

            _logger.LogInformation($"Received sampled payload from dataset with name {dataset.Name} in asset with name {assetName}. Now publishing it to MQTT broker: {Encoding.UTF8.GetString(serializedPayload)}");

            if (dataset.Destinations == null)
            {
                _logger.LogError("Cannot forward sampled dataset because it has no configured destinations");
                return;
            }

            foreach (var destination in dataset.Destinations)
            {
                if (destination.Target == DatasetTarget.Mqtt)
                {
                    var topic = destination.Configuration.Topic ??
                                throw new AssetConfigurationException(
                                    $"Dataset with name {dataset.Name} in asset with name {assetName} has no configured MQTT topic to publish to. Data won't be forwarded for this dataset.");

                    var messageMetadata = new OutgoingTelemetryMetadata
                    {
                        CloudEvent = aioCloudEvent?.ToCloudEvent(ApplicationContext.ApplicationHlc.Timestamp)
                    };

                    // Add AIO-specific extension attributes to UserData
                    if (aioCloudEvent != null)
                    {
                        foreach (var extension in aioCloudEvent.GetExtensions())
                        {
                            messageMetadata.UserData[extension.Key] = extension.Value;
                        }
                    }

                    Retain? retain = destination.Configuration.Retain;
                    if (retain != null)
                    {
                        messageMetadata.Retain = retain == Retain.Keep;
                    }

                    if (userData != null)
                    {
                        foreach (string key in userData.Keys)
                        {
                            messageMetadata.UserData[key] = userData[key];
                        }
                    }

                    TimeSpan? telemetryTimeout = null;
                    ulong? ttl = destination.Configuration.Ttl;
                    if (ttl != null && ttl.Value > 0)
                    {
                        telemetryTimeout = TimeSpan.FromSeconds(ttl.Value);
                    }

                    var telemetrySender = _telemetrySenderCache.GetOrAdd(topic, t => new ConnectorTelemetrySender(ApplicationContext, _mqttClient, t));
                    await telemetrySender.SendTelemetryAsync(
                        serializedPayload,
                        messageMetadata,
                        null,
                        destination.Configuration.Qos == null ? default : (MqttQualityOfServiceLevel)destination.Configuration.Qos,
                        telemetryTimeout,
                        cancellationToken);

                    _logger.LogInformation("Message was successfully sent to MQTT broker on topic {Topic}", topic);
                }
                else if (destination.Target == DatasetTarget.BrokerStateStore)
                {
                    await using StateStoreClient stateStoreClient = new(ApplicationContext, _mqttClient);

                    string stateStoreKey = destination.Configuration.Key ?? throw new AssetConfigurationException("Cannot publish sampled dataset to state store as it has no configured key");

                    IStateStoreSetResponse response = await stateStoreClient.SetAsync(stateStoreKey, new(serializedPayload), cancellationToken: cancellationToken);

                    if (response.Success)
                    {
                        _logger.LogInformation($"Message was accepted by the state store in key {stateStoreKey}");
                    }
                    else
                    {
                        _logger.LogError($"Message was not accepted by the state store");
                    }

                    await stateStoreClient.StopAsync(cancellationToken);
                }
                else if (destination.Target == DatasetTarget.Storage)
                {
                    throw new NotImplementedException();
                }
            }
        }

        // Called by AssetClient instances
        internal async Task ForwardReceivedEventAsync(string deviceName, Device device, string inboundEndpointName, string assetName, Asset asset,
            string eventGroupName, AssetEvent assetEvent, byte[] serializedPayload, Dictionary<string, string>? userData = null,
            string? protocolSpecificIdentifier = null, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _logger.LogInformation($"Received event with name {assetEvent.Name} in event group with name {eventGroupName} in asset with name {assetName}. Now publishing it to MQTT broker.");

            if (assetEvent.Destinations == null)
            {
                _logger.LogError("Cannot forward received event because it has no configured destinations");
                return;
            }

            Schema? registeredEventMessageSchema = null;
            if (!_registeredEventMessageSchemas.ContainsKey($"{deviceName}_{inboundEndpointName}_{assetName}_{eventGroupName}_{assetEvent.Name}"))
            {
                // This may register a message schema that has already been uploaded, but the schema registry service is idempotent
                var eventMessageSchema = await _messageSchemaProviderFactory.GetMessageSchemaAsync(device, asset, assetEvent.Name, assetEvent, cancellationToken);
                if (eventMessageSchema != null)
                {
                    try
                    {
                        _logger.LogInformation($"Registering message schema for event with name {assetEvent.Name} in event group with name {eventGroupName} on asset with name {assetName} associated with device with name {deviceName} and inbound endpoint name {inboundEndpointName}");
                        await using SchemaRegistryClient schemaRegistryClient = new(ApplicationContext, _mqttClient);
                        registeredEventMessageSchema = await schemaRegistryClient.PutAsync(
                            eventMessageSchema.SchemaContent,
                            eventMessageSchema.SchemaFormat,
                            eventMessageSchema.SchemaType,
                            eventMessageSchema.Version ?? "1",
                            eventMessageSchema.Tags,
                            cancellationToken: cancellationToken);

                        _logger.LogInformation($"Registered message schema for event with name {assetEvent.Name} on asset with name {assetName} associated with device with name {deviceName} and inbound endpoint name {inboundEndpointName}.");

                        _registeredEventMessageSchemas.TryAdd($"{deviceName}_{inboundEndpointName}_{assetName}_{eventGroupName}_{assetEvent.Name}", registeredEventMessageSchema);

                        await schemaRegistryClient.StopAsync(cancellationToken);
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

            CloudEvents.AioCloudEvent? aioCloudEvent = null;
            if (_registeredEventMessageSchemas.TryGetValue($"{deviceName}_{inboundEndpointName}_{assetName}_{eventGroupName}_{assetEvent.Name}", out registeredEventMessageSchema))
            {
                aioCloudEvent = ConstructCloudEventHeadersForEvent(
                    device,
                    deviceName,
                    inboundEndpointName,
                    asset,
                    assetName,
                    eventGroupName,
                    assetEvent,
                    registeredEventMessageSchema,
                    protocolSpecificIdentifier);
            }

            foreach (var destination in assetEvent.Destinations)
            {
                if (destination.Target == EventStreamTarget.Mqtt)
                {
                    string topic = destination.Configuration.Topic ??
                                   throw new AssetConfigurationException(
                                       $"Dataset with name {assetEvent.Name} in asset with name {assetName} has no configured MQTT topic to publish to. Data won't be forwarded for this dataset.");

                    var messageMetadata = new OutgoingTelemetryMetadata
                    {
                        CloudEvent = aioCloudEvent?.ToCloudEvent(ApplicationContext.ApplicationHlc.Timestamp)
                    };

                    // Add AIO-specific extension attributes to UserData
                    if (aioCloudEvent != null)
                    {
                        foreach (var extension in aioCloudEvent.GetExtensions())
                        {
                            messageMetadata.UserData[extension.Key] = extension.Value;
                        }
                    }

                    Retain? retain = destination.Configuration.Retain;
                    if (retain != null)
                    {
                        messageMetadata.Retain = retain == Retain.Keep;
                    }

                    if (userData != null)
                    {
                        foreach (string key in userData.Keys)
                        {
                            messageMetadata.UserData[key] = userData[key];
                        }
                    }

                    TimeSpan? telemetryTimeout = null;
                    ulong? ttl = destination.Configuration.Ttl;
                    if (ttl != null && ttl.Value > 0)
                    {
                        telemetryTimeout = TimeSpan.FromSeconds(ttl.Value);
                    }

                    var telemetrySender = _telemetrySenderCache.GetOrAdd(topic, t => new ConnectorTelemetrySender(ApplicationContext, _mqttClient, t));
                    await telemetrySender.SendTelemetryAsync(
                        serializedPayload,
                        messageMetadata,
                        null,
                        destination.Configuration.Qos == null ? default : (MqttQualityOfServiceLevel)destination.Configuration.Qos,
                        telemetryTimeout,
                        cancellationToken);

                    _logger.LogInformation("Message was successfully sent to MQTT broker on topic {Topic}", topic);
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

            try
            {
                Task.WhenAll(_telemetrySenderCache.Values.Select(ts => ts.DisposeAsync().AsTask()))
                    .GetAwaiter()
                    .GetResult();
            }
            catch (AggregateException ex)
            {
                foreach (var innerException in ex.InnerExceptions)
                {
                    _logger.LogError(innerException, "Error disposing telemetry sender");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing telemetry senders");
            }
            finally
            {
                _telemetrySenderCache.Clear();
                _isDisposed = true;
            }
        }
        private void OnAssetChanged(object? _, AssetChangedEventArgs args)
        {
            // Append to the per-asset chain so this runs only after the previous notification for the
            // same asset finishes. This serialization guarantees Created registers the runtime context
            // before any Updated runs (never forking a second AssetClient) and that Deleted tears down
            // only after in-flight work has drained.
            string key = GetCompoundAssetName($"{args.DeviceName}_{args.InboundEndpointName}", args.AssetName);
            lock (_assetNotificationChainLock)
            {
                Task previous = _assetNotificationChains.TryGetValue(key, out Task? p) ? p : Task.CompletedTask;
                Task current = ProcessAssetChangedAsync(args, previous);
                _assetNotificationChains[key] = current;
                PruneNotificationChainWhenComplete(_assetNotificationChains, _assetNotificationChainLock, key, current);
            }
        }

        private async Task ProcessAssetChangedAsync(AssetChangedEventArgs args, Task previous)
        {
            // A prior notification's failure must not block later ones, so swallow it here.
            try { await previous.ConfigureAwait(false); }
            catch { /* prior notification already logged its own failure */ }

            try
            {
                if (args.ChangeType == ChangeType.Created)
                {
                    _logger.LogInformation("Asset with name {0} created on endpoint with name {1} on device with name {2}", args.AssetName, args.InboundEndpointName, args.DeviceName);
                    AssetAvailable(args.DeviceName, args.InboundEndpointName, args.Asset, args.AssetName);
                    _adrClient!.ObserveAssets(args.DeviceName, args.InboundEndpointName);
                }
                else if (args.ChangeType == ChangeType.Deleted)
                {
                    _logger.LogInformation("Asset with name {0} deleted from endpoint with name {1} on device with name {2}", args.AssetName, args.InboundEndpointName, args.DeviceName);
                    await AssetUnavailableAsync(args.DeviceName, args.InboundEndpointName, args.AssetName);

                    // Note that the connector does not unsubscribe from notifications about this now-deleted asset. In the near future,
                    // the ADR service itself will do this for the connector. Trying to unsubscribe would yield a 404 from the ADR service
                    // since the asset the notifications were about no longer exists.
                }
                else if (args.ChangeType == ChangeType.Updated)
                {
                    _logger.LogInformation("Asset with name {0} updated on endpoint with name {1} on device with name {2}", args.AssetName, args.InboundEndpointName, args.DeviceName);
                    await AssetUpdatedAsync(args.DeviceName, args.InboundEndpointName, args.AssetName, args.Asset);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process {ChangeType} notification for asset {AssetName} on endpoint {InboundEndpointName} of device {DeviceName}", args.ChangeType, args.AssetName, args.InboundEndpointName, args.DeviceName);
            }
        }

        private void OnDeviceChanged(object? _, DeviceChangedEventArgs args)
        {
            // Serialize device notifications per device/endpoint for the same reason as assets.
            string key = $"{args.DeviceName}_{args.InboundEndpointName}";
            lock (_deviceNotificationChainLock)
            {
                Task previous = _deviceNotificationChains.TryGetValue(key, out Task? p) ? p : Task.CompletedTask;
                Task current = ProcessDeviceChangedAsync(args, previous);
                _deviceNotificationChains[key] = current;
                PruneNotificationChainWhenComplete(_deviceNotificationChains, _deviceNotificationChainLock, key, current);
            }
        }

        /// <summary>
        /// Remove <paramref name="key"/> from <paramref name="chains"/> once <paramref name="current"/>
        /// completes, but only if it is still the tail (otherwise a newer notification has appended to it).
        /// Without this, <paramref name="chains"/> would retain one completed task per name for the
        /// connector's lifetime.
        /// </summary>
        private static void PruneNotificationChainWhenComplete(
            Dictionary<string, Task> chains,
            object chainLock,
            string key,
            Task current)
        {
            _ = current.ContinueWith(
                _ =>
                {
                    lock (chainLock)
                    {
                        if (chains.TryGetValue(key, out Task? tail) && tail == current)
                        {
                            chains.Remove(key);
                        }
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        private async Task ProcessDeviceChangedAsync(DeviceChangedEventArgs args, Task previous)
        {
            try { await previous.ConfigureAwait(false); }
            catch { /* prior notification already logged its own failure */ }

            string compoundDeviceName = $"{args.DeviceName}_{args.InboundEndpointName}";
            try
            {
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
                                    await using var deviceAvailableEventArgs = new DeviceAvailableEventArgs(args.DeviceName, args.Device, args.InboundEndpointName, _leaderElectionClient, _adrClient!);
                                    await WhileDeviceIsAvailable.Invoke(deviceAvailableEventArgs, deviceTaskCancellationTokenSource.Token);
                                }
                                catch (OperationCanceledException)
                                {
                                    // This is the expected way for the callback to exit since this layer signals the cancellation token
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "User-supplied WhileDeviceIsAvailable callback for device {DeviceName} (endpoint {InboundEndpointName}) faulted", args.DeviceName, args.InboundEndpointName);
                                }
                            });

                            _deviceTasks.TryAdd(compoundDeviceName, new(userTask, deviceTaskCancellationTokenSource));
                        }
                    }
                }
                else if (args.ChangeType == ChangeType.Deleted)
                {
                    _logger.LogInformation("Device with name {0} and/or its endpoint with name {} was deleted", args.DeviceName, args.InboundEndpointName);
                    await DeviceUnavailableAsync(args, compoundDeviceName);
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
                    DeviceUpdated(args, compoundDeviceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process {ChangeType} notification for device {DeviceName} (endpoint {InboundEndpointName})", args.ChangeType, args.DeviceName, args.InboundEndpointName);
            }
        }

        /// <summary>
        /// Handle a device <see cref="ChangeType.Updated"/> notification. The device/endpoint
        /// identity is unchanged, so we refresh the cached <see cref="Device"/> snapshot in place
        /// without tearing anything down. Previously this path called
        /// <see cref="DeviceUnavailableAsync"/> followed by <see cref="DeviceAvailable"/>, which
        /// removed the device from <see cref="_devices"/> and cancelled every in-flight per-asset
        /// runtime (including the management-action orchestrator and its subscribed executors).
        /// During startup churn ADR can emit several spurious device Updated notifications in quick
        /// succession; tearing down on each one dropped executor subscriptions (transient
        /// <c>NoMatchingSubscribers</c>) and cancelled action loops mid-flight before they finished
        /// reporting status. Refreshing the snapshot keeps the asset runtime alive across updates.
        /// </summary>
        private void DeviceUpdated(DeviceChangedEventArgs args, string compoundDeviceName)
        {
            if (args.Device == null)
            {
                // shouldn't ever happen
                _logger.LogError("Received notification that device was updated, but no device was provided");
                return;
            }

            if (_devices.TryGetValue(compoundDeviceName, out DeviceContext? existing))
            {
                // Same device/endpoint identity: refresh the snapshot, keep the asset runtime running.
                existing.Device = args.Device;

                // When the user supplies no WhileDeviceIsAvailable callback, the SDK owns the device's
                // baseline status (idempotent). Re-publish so a device update keeps Config populated.
                if (WhileDeviceIsAvailable == null)
                {
                    _ = Task.Run(() => PublishInitialHealthyDeviceStatusAsync(args.DeviceName, args.InboundEndpointName));
                }
            }
            else
            {
                // We weren't tracking this device yet (Updated arrived before Created, or after a
                // prior teardown). Treat it as newly available.
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

                // Device status is a device-lifecycle concern. When no WhileDeviceIsAvailable callback
                // reports it, the SDK owns the baseline so DeviceStatus.Config never stays null. Idempotent.
                if (WhileDeviceIsAvailable == null)
                {
                    _ = Task.Run(() => PublishInitialHealthyDeviceStatusAsync(args.DeviceName, args.InboundEndpointName));
                }
            }
        }

        /// <summary>
        /// Publish a healthy <see cref="DeviceStatus"/> (Config + per-inbound-endpoint entry, both
        /// with <c>Error = null</c>) for the given device endpoint. Best-effort: any failure is
        /// logged and swallowed so it can't fault the notification thread or block subsequent
        /// asset processing. Idempotent &mdash; safe to call repeatedly (e.g. on Created and Updated).
        /// </summary>
        private async Task PublishInitialHealthyDeviceStatusAsync(string deviceName, string inboundEndpointName)
        {
            try
            {
                DeviceStatus current = await _adrClient!.GetDeviceStatusAsync(deviceName, inboundEndpointName);
                current.Config ??= new();
                current.Config.LastTransitionTime = DateTime.UtcNow;
                current.Config.Error = null;
                current.Endpoints ??= new();
                current.Endpoints.Inbound ??= new();
                if (!current.Endpoints.Inbound.ContainsKey(inboundEndpointName))
                {
                    current.Endpoints.Inbound[inboundEndpointName] = new();
                }
                current.Endpoints.Inbound[inboundEndpointName].Error = null;

                await _adrClient.UpdateDeviceStatusAsync(deviceName, inboundEndpointName, current);
                _logger.LogInformation(
                    "Reported initial healthy device status for device {DeviceName} (endpoint {InboundEndpointName})",
                    deviceName, inboundEndpointName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish initial healthy device status for device {DeviceName} (endpoint {InboundEndpointName})",
                    deviceName, inboundEndpointName);
            }
        }

        /// <summary>
        /// Publish a healthy <see cref="AssetStatus"/> baseline (<c>Config.Error = null</c>) so an
        /// asset's <see cref="AssetStatus.Config"/> is never left null when the SDK owns its status.
        /// Routed through <see cref="AssetClient.GetAndUpdateAssetStatusAsync"/> (not the ADR client
        /// directly) so it shares the per-asset mutex and last-written-status cache with the
        /// management-action orchestrator and can't clobber per-action error reporting. Only touches
        /// <c>Config</c>; leaves Datasets/Events/Streams/ManagementGroups intact. Best-effort and
        /// idempotent (<c>onlyIfChanged: true</c>): safe to call on both Created and Updated.
        /// </summary>
        private async Task PublishInitialHealthyAssetStatusAsync(AssetClient assetClient, string assetName)
        {
            try
            {
                await assetClient.GetAndUpdateAssetStatusAsync(
                    current =>
                    {
                        current.Config ??= new();
                        current.Config.LastTransitionTime = DateTime.UtcNow;
                        current.Config.Error = null;
                        return current;
                    },
                    onlyIfChanged: true);
                _logger.LogInformation("Reported initial healthy asset status for asset {AssetName}", assetName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish initial healthy asset status for asset {AssetName}", assetName);
            }
        }

        private async Task DeviceUnavailableAsync(DeviceChangedEventArgs args, string compoundDeviceName)
        {
            await _adrClient!.UnobserveAssetsAsync(args.DeviceName, args.InboundEndpointName);

            if (_devices.TryRemove(compoundDeviceName, out var deviceContext))
            {
                foreach (string assetName in deviceContext.Assets.Keys)
                {
                    await AssetUnavailableAsync(args.DeviceName, args.InboundEndpointName, assetName);
                }
            }
        }

        private void AssetAvailable(string deviceName, string inboundEndpointName, Asset? asset, string assetName)
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

            if (asset.EventGroups == null)
            {
                _logger.LogInformation($"Asset with name {assetName} has no events to listen for");
            }

            if (WhileAssetIsAvailable == null && _managementActionOrchestrator == null)
            {
                return;
            }

            // Long-lived AssetClient (survives Updated events, drives the MA branch). The runtime context
            // owns it; the MA-branch args and the user-callback args (rebuilt on each Updated for a fresh
            // token) merely wrap it without taking ownership.
            AssetClient assetClient = new(_adrClient!, deviceName, inboundEndpointName, assetName, this, device, asset);
            AssetAvailableEventArgs managementActionArgs = new(deviceName, device, inboundEndpointName, assetName, asset, _leaderElectionClient, _adrClient!, assetClient);
            CancellationTokenSource managementActionCts = new();
            CancellationTokenSource userCts = new();

            // Reserve the per-asset slot BEFORE starting any branch. Per-asset serialization makes this
            // TryAdd the single enforcer of "exactly one AssetClient per asset".
            // Disposing the loser here guarantees it never touches ADR.
            AssetRuntimeContext assetRuntimeCtx = new(
                assetClient: assetClient,
                managementActionArgs: managementActionArgs,
                managementActionCts: managementActionCts,
                managementActionTask: null,
                userCts: userCts,
                userTask: null,
                userArgs: null);

            if (!_assetTasks.TryAdd(GetCompoundAssetName(compoundDeviceName, assetName), assetRuntimeCtx))
            {
                // Invariant violation: the prior context should have been removed (Deleted) before
                // AssetAvailable runs again for the same asset. Bail out without starting any branch.
                _logger.LogWarning(
                    "Asset {AssetName} on device {DeviceName} (endpoint {InboundEndpointName}) is already being tracked; discarding duplicate runtime context before any branch starts. This indicates the per-asset notification serialization invariant was violated.",
                    assetName, deviceName, inboundEndpointName);
                managementActionCts.Dispose();
                userCts.Dispose();
                // Dispose the orphan args and AssetClient. No branch started, nothing to await.
                _ = managementActionArgs.DisposeAsync();
                _ = assetClient.DisposeAsync();
                return;
            }

            // We own the slot: start the branches now.
            if (_managementActionOrchestrator != null
                && asset.ManagementGroups?.Any(g => g.Actions?.Count > 0) == true)
            {
                Task managementActionTask = Task.Run(() => SafeInvokeAssetBranchAsync(
                    "ManagementActionHandling",
                    ct => _managementActionOrchestrator.ServeActionsWhileAssetIsAvailableAsync(managementActionArgs, ct),
                    managementActionCts.Token,
                    assetName, deviceName, inboundEndpointName));
                assetRuntimeCtx.AttachManagementActionTask(managementActionTask);
            }

            if (WhileAssetIsAvailable != null)
            {
                AssetAvailableEventArgs userArgs = new(deviceName, device, inboundEndpointName, assetName, asset, _leaderElectionClient, _adrClient!, assetClient);
                Task userTask = Task.Run(() => SafeInvokeAssetBranchAsync(
                    "UserWhileAssetIsAvailableCallback",
                    ct => WhileAssetIsAvailable!.Invoke(userArgs, ct),
                    userCts.Token,
                    assetName, deviceName, inboundEndpointName));
                assetRuntimeCtx.SwapUserBranch(userCts, userTask, userArgs);
            }

            // When the user supplies no WhileAssetIsAvailable callback, the SDK owns the asset's
            // baseline status. Publish a healthy Config so AssetStatus.Config never stays null even
            // when the asset has no (or not-yet-validated) management actions. Idempotent; fire-and-forget.
            if (WhileAssetIsAvailable == null)
            {
                _ = Task.Run(() => PublishInitialHealthyAssetStatusAsync(assetClient, assetName));
            }
        }

        /// <summary>
        /// Handle an asset update: preserves the long-lived <see cref="AssetClient"/> (so management-action
        /// state survives), pushes diff notifications via <see cref="AssetClient.ApplyAssetUpdateAsync"/>,
        /// and cancels+rebuilds the user <see cref="WhileAssetIsAvailable"/> branch with a fresh
        /// <see cref="CancellationToken"/>. The management-action branch keeps running across the update.
        /// </summary>
        private async Task AssetUpdatedAsync(string deviceName, string inboundEndpointName, string assetName, Asset? newAsset)
        {
            string compoundDeviceName = $"{deviceName}_{inboundEndpointName}";
            string compoundAssetName = GetCompoundAssetName(compoundDeviceName, assetName);

            if (newAsset is null)
            {
                _logger.LogError("Received Updated notification for asset {AssetName} with no asset payload; ignoring", assetName);
                return;
            }

            if (!_assetTasks.TryGetValue(compoundAssetName, out AssetRuntimeContext? assetRuntimeContext))
            {
                // No live runtime for this asset (no callback/orchestrator configured, or Created was
                // never seen). Treat the update as a create so bookkeeping stays consistent.
                AssetAvailable(deviceName, inboundEndpointName, newAsset, assetName);
                return;
            }

            // Refresh the device-level snapshot for the rest of the worker.
            if (_devices.TryGetValue(compoundDeviceName, out DeviceContext? deviceContext))
            {
                deviceContext.Assets[assetName] = newAsset;
            }

            // 1) Push the diff into the per-action channels and notify the orchestrator. ApplyAssetUpdateAsync
            //    swaps cached executors when an action's request topic changes; the MA branch keeps running.
            try
            {
                await assetRuntimeContext.AssetClient.ApplyAssetUpdateAsync(newAsset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply asset update for {AssetName}; user callback will be rebuilt anyway", assetName);
            }

            // 2) Cancel and tear down the previous user-callback branch.
            CancellationTokenSource oldUserCts = assetRuntimeContext.UserCts;
            Task? oldUserTask = assetRuntimeContext.UserTask;
            AssetAvailableEventArgs? oldUserArgs = assetRuntimeContext.UserArgs;

            try { oldUserCts.Cancel(); } catch (ObjectDisposedException) { }
            if (oldUserTask is not null)
            {
                try { await oldUserTask; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "User WhileAssetIsAvailable callback for asset {AssetName} faulted during update tear-down", assetName);
                }
            }
            try { oldUserCts.Dispose(); } catch { /* best-effort */ }
            if (oldUserArgs is not null)
            {
                try { await oldUserArgs.DisposeAsync(); } catch { /* borrow-mode args; safe */ }
            }

            // 3) Rebuild the user-callback branch with a fresh CTS and fresh borrow-mode args.
            CancellationTokenSource newUserCts = new();
            AssetAvailableEventArgs? newUserArgs = null;
            Task? newUserTask = null;

            Device? device = deviceContext?.Device;
            if (WhileAssetIsAvailable != null && device != null)
            {
                newUserArgs = new AssetAvailableEventArgs(deviceName, device, inboundEndpointName, assetName, newAsset, _leaderElectionClient, _adrClient!, assetRuntimeContext.AssetClient);
                AssetAvailableEventArgs capturedArgs = newUserArgs;
                newUserTask = Task.Run(() => SafeInvokeAssetBranchAsync(
                    "UserWhileAssetIsAvailableCallback",
                    ct => WhileAssetIsAvailable!.Invoke(capturedArgs, ct),
                    newUserCts.Token,
                    assetName, deviceName, inboundEndpointName));
            }

            assetRuntimeContext.SwapUserBranch(newUserCts, newUserTask, newUserArgs);

            // SDK owns the asset baseline when there's no user callback; re-publish so an asset
            // update keeps Config populated. Idempotent; fire-and-forget.
            if (WhileAssetIsAvailable == null)
            {
                _ = Task.Run(() => PublishInitialHealthyAssetStatusAsync(assetRuntimeContext.AssetClient, assetName));
            }
        }

        /// <summary>
        /// Runs one of the per-asset branches (the user's <see cref="WhileAssetIsAvailable"/>
        /// callback or the built-in management-action loop) and isolates its failures so a
        /// fault in one branch doesn't fault the other on the shared <c>Task.WhenAll</c>.
        /// </summary>
        private async Task SafeInvokeAssetBranchAsync(
            string label,
            Func<CancellationToken, Task> body,
            CancellationToken cancellationToken,
            string assetName,
            string deviceName,
            string inboundEndpointName)
        {
            try
            {
                await body(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // This is the expected way for the callback to exit since this layer signals the cancellation token
            }
            catch (Exception ex)
            {
                // Surface failures so the task doesn't fault silently inside Task.WhenAll.
                _logger.LogError(ex, "{Label} for asset {AssetName} on device {DeviceName} (endpoint {InboundEndpointName}) faulted", label, assetName, deviceName, inboundEndpointName);
            }
        }

        private async Task AssetUnavailableAsync(string deviceName, string inboundEndpointName, string assetName)
        {
            string compoundDeviceName = $"{deviceName}_{inboundEndpointName}";

            // This method is only called when an asset is deleted (or its parent device is going
            // away). Updated is handled by AssetUpdatedAsync; this path always tears down the
            // long-lived AssetClient and both branches.
            if (_assetTasks.TryRemove(GetCompoundAssetName(compoundDeviceName, assetName), out AssetRuntimeContext? ctx))
            {
                // Cancel user branch first so the user callback observes shutdown before MA loops
                // start tearing down their executors (matches the original ordering: user code is
                // the most user-visible cancellation, MA framework finishes draining after).
                try { ctx.UserCts.Cancel(); } catch (ObjectDisposedException) { }
                try { ctx.ManagementActionCts.Cancel(); } catch (ObjectDisposedException) { }

                try
                {
                    if (ctx.UserTask is not null) await ctx.UserTask;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Encountered an exception while cancelling user-defined task for device name {deviceName}, inbound endpoint name {inboundEndpointName}, asset name {assetName}", deviceName, inboundEndpointName, assetName);
                }

                try
                {
                    if (ctx.ManagementActionTask is not null) await ctx.ManagementActionTask;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Encountered an exception while cancelling management-action task for device name {deviceName}, inbound endpoint name {inboundEndpointName}, asset name {assetName}", deviceName, inboundEndpointName, assetName);
                }

                try { await ctx.DisposeAsync(); }
                catch (Exception e)
                {
                    _logger.LogError(e, "Encountered an exception while disposing asset runtime context for device name {deviceName}, inbound endpoint name {inboundEndpointName}, asset name {assetName}", deviceName, inboundEndpointName, assetName);
                }
            }
        }

        private string GetCompoundAssetName(string compoundDeviceName, string assetName)
        {
            return compoundDeviceName + "_" + assetName;
        }

        internal AioCloudEvent? ConstructCloudEventHeadersForDataset(Device device,
            string deviceName,
            string inboundEndpointName,
            Asset asset,
            string assetName,
            AssetDataset dataset,
            Schema registeredSchema,
            string? protocolSpecificIdentifier = null)
        {
            try
            {
                var schemaRef = new MessageSchemaReference
                {
                    SchemaRegistryNamespace = registeredSchema.Namespace,
                    SchemaName = registeredSchema.Name,
                    SchemaVersion = registeredSchema.Version
                };

                return AioCloudEventBuilder.Build(
                    device,
                    deviceName,
                    inboundEndpointName,
                    asset,
                    dataset,
                    assetName,
                    protocolSpecificIdentifier,
                    schemaRef);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to construct CloudEvents headers for dataset {dataset.Name}");
                return null;
            }
        }

        internal AioCloudEvent? ConstructCloudEventHeadersForEvent(Device device,
            string deviceName,
            string inboundEndpointName,
            Asset asset,
            string assetName,
            string eventGroupName,
            AssetEvent assetEvent,
            Schema registeredSchema,
            string? protocolSpecificIdentifier = null)
        {
            try
            {
                var schemaRef = new MessageSchemaReference
                {
                    SchemaRegistryNamespace = registeredSchema.Namespace,
                    SchemaName = registeredSchema.Name,
                    SchemaVersion = registeredSchema.Version
                };

                return AioCloudEventBuilder.Build(
                    device,
                    deviceName,
                    inboundEndpointName,
                    asset,
                    assetEvent,
                    assetName,
                    eventGroupName,
                    protocolSpecificIdentifier,
                    schemaRef);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to construct CloudEvents headers for event {assetEvent.Name}");
                return null;
            }
        }
    }
}
