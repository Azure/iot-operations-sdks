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

        // Asset-available notifications can arrive before (or racing with) the device-available
        // notification that populates _devices (startup churn, or re-observe after a device update).
        // Rather than dropping such notifications permanently, we buffer them here keyed by
        // "<deviceName>_<inboundEndpointName>" and replay them once the owning device is available.
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Asset>> _pendingAssets = new();

        private bool _isDisposed = false;
        private readonly ConnectorLeaderElectionConfiguration? _leaderElectionConfiguration;
        private readonly ConcurrentDictionary<string, ConnectorTelemetrySender> _telemetrySenderCache = new();

        // Keys are <deviceName>_<inboundEndpointName> and values are the running task and their cancellation token to signal once the device is no longer available or the connector is shutting down
        private readonly ConcurrentDictionary<string, UserTaskContext> _deviceTasks = new();

        // Keys are <deviceName>_<inboundEndpointName>_<assetName>. Holds the long-lived per-asset
        // runtime state: the AssetClient (preserved across asset Updated events), the
        // management-action branch task (cancelled only on Deleted), and the user-callback branch
        // (cancelled and rebuilt on every Updated so the user gets a fresh CancellationToken).
        private readonly ConcurrentDictionary<string, AssetRuntimeContext> _assetTasks = new();

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

        // TODO (refactor, follow-up PR): Replace the back-pointer that AssetClient holds to
        // ConnectorWorker (and the two internal accessors below) with a narrow seam, e.g.
        //
        //     internal interface IConnectorRuntime
        //     {
        //         ApplicationContext ApplicationContext { get; }
        //         IMqttPubSubClient  MqttPubSubClient   { get; }
        //         Task ForwardSampledDatasetAsync(...);
        //         Task ForwardReceivedEventAsync(...);
        //         MessageSchemaReference? GetRegisteredDatasetMessageSchema(...);
        //         MessageSchemaReference? GetRegisteredEventMessageSchema(...);
        //     }
        //
        // Today AssetClient takes a `ConnectorWorker _connector` and reaches through it for
        // ForwardSampledDatasetAsync / ForwardReceivedEventAsync / GetRegisteredXxxMessageSchema
        // as well as the two accessors below (added for Workstream B so AssetClient can build
        // a ManagementActionExecutor on the worker's shared MQTT session). That's a
        // service-locator / Law-of-Demeter smell: AssetClient doesn't advertise what it
        // depends on, and it's hard to unit-test in isolation (you need a real or mocked
        // ConnectorWorker just to construct one).
        //
        // The fix is to declare IConnectorRuntime, have ConnectorWorker implement it, and
        // change AssetClient's ctor + field to IConnectorRuntime. That keeps the runtime
        // call paths intact, makes AssetClient mockable, and gives a single chokepoint for
        // any future "AssetClient needs something else from the worker" requests instead of
        // growing more internal properties on ConnectorWorker over time.
        //
        // Out of scope for the management-action workstream; tracked here so it doesn't get
        // lost. See doc/dev/tmp/management-action-implementation-design.md (deviations).

        /// <summary>
        /// Shared <see cref="ApplicationContext"/> (HLC, etc.) exposed to internal SDK
        /// collaborators (e.g. <see cref="AssetClient.GetManagementActionExecutorAsync"/>)
        /// that need to construct protocol clients on top of the worker's MQTT session.
        /// </summary>
        internal ApplicationContext ApplicationContext { get; }

        /// <summary>
        /// Shared MQTT pub/sub client exposed to internal SDK collaborators
        /// (e.g. <see cref="AssetClient.GetManagementActionExecutorAsync"/>) that need to
        /// build executors/senders on top of the worker's MQTT session.
        /// </summary>
        internal IMqttPubSubClient MqttPubSubClient => _mqttClient;

        /// <summary>
        /// Logger exposed to internal SDK collaborators (e.g. <see cref="AssetClient"/>) so they
        /// can emit diagnostics without each constructing their own logger.
        /// </summary>
        internal ILogger Logger => _logger;

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
                    try { assetCtx.MaCts.Cancel(); } catch (ObjectDisposedException) { }

                    if (assetCtx.UserTask is not null)
                    {
                        tasksToAwait.Add(assetCtx.UserTask);
                    }
                    if (assetCtx.MaTask is not null)
                    {
                        tasksToAwait.Add(assetCtx.MaTask);
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
        private async void OnAssetChanged(object? _, AssetChangedEventArgs args)
        {
            string compoundDeviceName = $"{args.DeviceName}_{args.InboundEndpointName}";
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

                // Management-action-only connectors take responsibility for publishing healthy device
                // status themselves (idempotent). Re-publish so a device update keeps Config populated.
                if (_managementActionOrchestrator != null && WhileDeviceIsAvailable == null)
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

                // An asset-available notification can arrive before the device that owns it is
                // registered (startup churn). Such assets are buffered; replay them now.
                ReplayPendingAssets(args.DeviceName, args.InboundEndpointName, compoundDeviceName);

                // Polling/event connectors publish initial healthy device status through their
                // user-supplied WhileDeviceIsAvailable callback (see PollingTelemetryConnectorWorker).
                // Management-action-only connectors typically don't supply that callback, so without
                // this branch DeviceStatus.Config stays null forever and downstream consumers
                // (e.g. AzureDeviceRegistryClient.GetDeviceStatusAsync) only see "Config":null.
                // When a ManagementAction orchestrator is wired up but no user device callback is
                // provided, take responsibility for publishing the initial healthy status ourselves.
                if (_managementActionOrchestrator != null && WhileDeviceIsAvailable == null)
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
                    "Reported initial healthy device status for device {DeviceName} (endpoint {InboundEndpointName}) on behalf of the management-action orchestrator",
                    deviceName, inboundEndpointName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish initial healthy device status for device {DeviceName} (endpoint {InboundEndpointName})",
                    deviceName, inboundEndpointName);
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
                // The device that owns this asset isn't registered yet (asset-available raced ahead
                // of device-available, e.g. during startup churn). Buffer the asset and replay it
                // when the device becomes available instead of dropping it permanently.
                ConcurrentDictionary<string, Asset> pending = _pendingAssets.GetOrAdd(compoundDeviceName, _ => new());
                pending[assetName] = asset;
                _logger.LogWarning("Received notification of asset with name {} becoming available on device {} with inbound endpoint name {}, but that device and/or inbound endpoint is not available yet. Buffering this asset until the device becomes available.", assetName, deviceName, inboundEndpointName);

                // The device may have been registered concurrently between the check above and the
                // buffer write. If so, reclaim the asset and process it now so it isn't stranded.
                if (!_devices.TryGetValue(compoundDeviceName, out deviceContext) || !pending.TryRemove(assetName, out asset!))
                {
                    return;
                }
                // else: device is now available and we reclaimed the asset; fall through to process it.
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

            // Long-lived per-asset runtime context. `ownedArgs` keeps the AssetClient alive across
            // any future Updated events; it is the args passed to the management-action branch.
            // The user-callback branch gets a separate `userArgs` (borrow mode) that we rebuild on
            // each Updated so the user callback sees a fresh CancellationToken.
            AssetAvailableEventArgs ownedArgs = new(deviceName, device, inboundEndpointName, assetName, asset, _leaderElectionClient, _adrClient!, this);

            CancellationTokenSource maCts = new();
            CancellationTokenSource userCts = new();

            Task? maTask = null;
            if (_managementActionOrchestrator != null
                && asset.ManagementGroups?.Any(g => g.Actions?.Count > 0) == true)
            {
                maTask = Task.Run(() => SafeInvokeAssetBranchAsync(
                    "ManagementActionHandling",
                    ct => _managementActionOrchestrator.ServeActionsWhileAssetIsAvailableAsync(ownedArgs, ct),
                    maCts.Token,
                    assetName, deviceName, inboundEndpointName));
            }

            AssetAvailableEventArgs? userArgs = null;
            Task? userTask = null;
            if (WhileAssetIsAvailable != null)
            {
                userArgs = new AssetAvailableEventArgs(deviceName, device, inboundEndpointName, assetName, asset, _leaderElectionClient, _adrClient!, ownedArgs.AssetClient);
                AssetAvailableEventArgs capturedUserArgs = userArgs;
                userTask = Task.Run(() => SafeInvokeAssetBranchAsync(
                    "UserWhileAssetIsAvailableCallback",
                    ct => WhileAssetIsAvailable!.Invoke(capturedUserArgs, ct),
                    userCts.Token,
                    assetName, deviceName, inboundEndpointName));
            }

            AssetRuntimeContext ctx = new(
                assetClient: ownedArgs.AssetClient,
                ownedArgs: ownedArgs,
                maCts: maCts,
                maTask: maTask,
                userCts: userCts,
                userTask: userTask,
                userArgs: userArgs);

            _assetTasks.TryAdd(GetCompoundAssetName(compoundDeviceName, assetName), ctx);
        }

        /// <summary>
        /// Replay any asset-available notifications that were buffered while the device was not yet
        /// registered. Called from <see cref="DeviceAvailable"/> once the device is in
        /// <see cref="_devices"/>, so each replayed <see cref="AssetAvailable"/> call now finds its
        /// owning device and processes normally instead of being dropped.
        /// </summary>
        private void ReplayPendingAssets(string deviceName, string inboundEndpointName, string compoundDeviceName)
        {
            if (!_pendingAssets.TryRemove(compoundDeviceName, out ConcurrentDictionary<string, Asset>? pending))
            {
                return;
            }

            foreach (string assetName in pending.Keys)
            {
                if (pending.TryRemove(assetName, out Asset? bufferedAsset))
                {
                    _logger.LogInformation("Replaying buffered asset {} on device {} (endpoint {}) now that the device is available.", assetName, deviceName, inboundEndpointName);
                    AssetAvailable(deviceName, inboundEndpointName, bufferedAsset, assetName);
                }
            }
        }

        /// <summary>
        /// Handle an asset update: preserves the long-lived <see cref="AssetClient"/> (so
        /// management-action handler state survives), pushes diff notifications via
        /// <see cref="AssetClient.ApplyAssetUpdateAsync"/>, and cancels + rebuilds the
        /// user-supplied <see cref="WhileAssetIsAvailable"/> callback branch so the user sees a
        /// fresh <see cref="CancellationToken"/>. The management-action orchestrator branch keeps
        /// running across the update.
        /// </summary>
        private async Task AssetUpdatedAsync(string deviceName, string inboundEndpointName, string assetName, Asset? newAsset)
        {
            string compoundDeviceName = $"{deviceName}_{inboundEndpointName}";
            string key = GetCompoundAssetName(compoundDeviceName, assetName);

            if (newAsset is null)
            {
                _logger.LogError("Received Updated notification for asset {AssetName} with no asset payload; ignoring", assetName);
                return;
            }

            if (!_assetTasks.TryGetValue(key, out AssetRuntimeContext? ctx))
            {
                // We weren't tracking it (e.g. user supplied neither a WhileAssetIsAvailable nor an
                // action-handler factory). Fall back to the create path so the rest of the worker
                // (device.Assets dictionary, etc.) stays consistent.
                AssetAvailable(deviceName, inboundEndpointName, newAsset, assetName);
                return;
            }

            // Refresh the device-level snapshot for the rest of the worker.
            if (_devices.TryGetValue(compoundDeviceName, out DeviceContext? deviceContext))
            {
                deviceContext.Assets[assetName] = newAsset;
            }

            // 1) Push the diff into the per-action channels and notify the orchestrator's outer
            //    loop. ApplyAssetUpdateAsync is responsible for swapping cached executors when an
            //    action's request topic changes; the MA branch keeps running and consumes the
            //    notifications without restart.
            try
            {
                await ctx.AssetClient.ApplyAssetUpdateAsync(newAsset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply asset update for {AssetName}; user callback will be rebuilt anyway", assetName);
            }

            // 2) Cancel and tear down the previous user-callback branch.
            CancellationTokenSource oldUserCts = ctx.UserCts;
            Task? oldUserTask = ctx.UserTask;
            AssetAvailableEventArgs? oldUserArgs = ctx.UserArgs;

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
                newUserArgs = new AssetAvailableEventArgs(deviceName, device, inboundEndpointName, assetName, newAsset, _leaderElectionClient, _adrClient!, ctx.AssetClient);
                AssetAvailableEventArgs capturedArgs = newUserArgs;
                newUserTask = Task.Run(() => SafeInvokeAssetBranchAsync(
                    "UserWhileAssetIsAvailableCallback",
                    ct => WhileAssetIsAvailable!.Invoke(capturedArgs, ct),
                    newUserCts.Token,
                    assetName, deviceName, inboundEndpointName));
            }

            ctx.SwapUserBranch(newUserCts, newUserTask, newUserArgs);
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
                try { ctx.MaCts.Cancel(); } catch (ObjectDisposedException) { }

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
                    if (ctx.MaTask is not null) await ctx.MaTask;
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
