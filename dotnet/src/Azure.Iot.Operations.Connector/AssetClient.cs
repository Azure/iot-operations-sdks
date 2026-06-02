// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Threading.Channels;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A client for updating the status of an asset and for forwarding received events and/or sampled datasets.
    /// </summary>
    public class AssetClient : IAsyncDisposable
    {
        private readonly IAzureDeviceRegistryClientWrapper _adrClient;
        private readonly ConnectorWorker _connector;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;
        private readonly string _assetName;
        private readonly Device _device;

        // Mutable: replaced by ApplyAssetUpdateAsync when the parent asset is updated, so that
        // the AssetClient instance can outlive a single asset revision. Writes are serialized via
        // _assetUpdateMutex below; reads from arbitrary public methods (e.g. ForwardSampledDatasetAsync)
        // and lock-free paths (CurrentAsset, BuildAndStartExecutorAsync, health reporting) observe
        // whatever revision is current. Reference assignment is atomic, so reads never tear; volatile
        // guarantees a reader promptly observes the latest reference without taking _assetUpdateMutex.
        private volatile Asset _asset;
        private readonly AssetRuntimeHealthReporter _healthReporter;

        // Used to make getAndUpdate calls behave atomically so that a user does not accidentally update
        // an asset while another thread is in the middle of a getAndUpdate call.
        private readonly SemaphoreSlim _statusUpdateMutex = new(1, 1);

        // The last status this client successfully wrote to ADR. This connector is the sole authoritative
        // writer of its own asset status, so we use our local copy as the base for read-modify-write
        // cycles instead of re-reading: ADR's get-after-put is not read-your-writes consistent, so
        // re-reading would let _statusUpdateMutex-serialized reports clobber each other's contributions (e.g.
        // three actions on one asset each reading a status missing the others' writes). Guarded by _statusUpdateMutex.
        private AssetStatus? _lastWrittenStatus;

        // Per-(group, action) state for the management-action API. Lazily populated on first access.
        private readonly ConcurrentDictionary<(string Group, string Action), ManagementActionState> _managementActionStates = new();

        // Serializes ApplyAssetUpdateAsync against itself and per-action state mutations so a concurrent
        // Get + Update can't race on _asset / _managementActionStates.
        private readonly SemaphoreSlim _assetUpdateMutex = new(1, 1);

        // Signals the ManagementActionOrchestrator to discover newly-added actions after an asset update.
        private readonly Channel<Asset> _assetUpdatedSignal = Channel.CreateUnbounded<Asset>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        internal AssetClient(IAzureDeviceRegistryClientWrapper adrClient, string deviceName, string inboundEndpointName, string assetName, ConnectorWorker connector, Device device, Asset asset)
        {
            _adrClient = adrClient;
            _deviceName = deviceName;
            _inboundEndpointName = inboundEndpointName;
            _assetName = assetName;
            _connector = connector;
            _device = device;
            _asset = asset;
            _healthReporter = new(adrClient.GetWrapped(), deviceName, inboundEndpointName, assetName, TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Get the current status of this asset and then optionally update it.
        /// </summary>
        /// <param name="handler">The function that determines the new asset status when given the current asset status.</param>
        /// <param name="onlyIfChanged">
        /// Only send the status update if the new status is different from the current status. If the only
        /// difference between the current and new status is a 'LastTransitionTime' field, then the statuses will be
        /// considered identical.
        /// </param>
        /// <param name="commandTimeout">The timeout for each of the 'get' and 'update' commands.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The latest asset status after this operation.</returns>
        /// <remarks>
        /// If after retrieving the current status, you don't want to send any updates, <paramref name="handler"/> should return null.
        /// If this happens, this function will return the latest asset status without trying to update it.
        ///
        /// This method uses a semaphore to ensure that this same client doesn't accidentally update the asset status while
        /// another thread is in the middle of updating the same asset. This ensures that the current device status provided in <paramref name="handler"/>
        /// stays accurate while any updating occurs.
        ///
        /// <paramref name="handler"/> may safely mutate the provided status in place and return that same instance.
        /// When <paramref name="onlyIfChanged"/> is true, change detection compares the handler's result against a
        /// snapshot of the status taken before the handler runs, so in-place mutation is detected correctly.
        /// </remarks>
        public async Task<AssetStatus> GetAndUpdateAssetStatusAsync(Func<AssetStatus, AssetStatus?> handler, bool onlyIfChanged = false, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            await _statusUpdateMutex.WaitAsync(cancellationToken);
            try
            {
                // Prefer our last-written status as the base: ADR's get-after-put is not read-your-writes
                // consistent, so re-reading here can drop concurrent serialized contributions.
                AssetStatus currentStatus = _lastWrittenStatus?.DeepClone()
                    ?? await GetAssetStatusAsync(commandTimeout, cancellationToken);

                // Snapshot before invoking the handler: handlers commonly mutate the status in place and
                // return the same reference, so comparing against the live object would always report "no change".
                AssetStatus originalStatus = currentStatus.DeepClone();
                AssetStatus? desiredStatus = handler.Invoke(currentStatus);
                bool changed = desiredStatus != null && (!onlyIfChanged || !originalStatus.EqualTo(desiredStatus));
                if (changed)
                {
                    AssetStatus updatedStatus = await UpdateAssetStatusAsync(desiredStatus!, commandTimeout, cancellationToken);
                    _lastWrittenStatus = desiredStatus!.DeepClone();
                    return updatedStatus;
                }

                return currentStatus;
            }
            finally
            {
                _statusUpdateMutex.Release();
            }
        }

        /// <summary>
        /// Push a sampled dataset to the configured destinations.
        /// </summary>
        /// <param name="dataset">The dataset that was sampled.</param>
        /// <param name="serializedPayload">The payload to push to the configured destinations.</param>
        /// <param name="userData">Optional headers to include in the telemetry. Only applicable for datasets with a destination of the MQTT broker.</param>
        /// <param name="protocolSpecificIdentifier">Optional protocol specific identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ForwardSampledDatasetAsync(AssetDataset dataset, byte[] serializedPayload, Dictionary<string, string>? userData = null, string? protocolSpecificIdentifier = null, CancellationToken cancellationToken = default)
        {
            await _connector.ForwardSampledDatasetAsync(_deviceName, _device, _inboundEndpointName, _assetName, _asset, dataset, serializedPayload, userData, protocolSpecificIdentifier, cancellationToken);
        }

        /// <summary>
        /// Push a received event payload to the configured destinations.
        /// </summary>
        /// <param name="eventGroupName">The name of the event group that this event belongs to.</param>
        /// <param name="assetEvent">The event.</param>
        /// <param name="serializedPayload">The payload to push to the configured destinations.</param>
        /// <param name="userData">Optional headers to include in the telemetry. Only applicable for datasets with a destination of the MQTT broker.</param>
        /// <param name="protocolSpecificIdentifier">Optional protocol specific identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ForwardReceivedEventAsync(string eventGroupName, AssetEvent assetEvent, byte[] serializedPayload, Dictionary<string, string>? userData = null, string? protocolSpecificIdentifier = null, CancellationToken cancellationToken = default)
        {
            await _connector.ForwardReceivedEventAsync(_deviceName, _device, _inboundEndpointName, _assetName, _asset, eventGroupName, assetEvent, serializedPayload, userData, protocolSpecificIdentifier, cancellationToken);
        }

        public MessageSchemaReference? GetRegisteredDatasetMessageSchema(string datasetName)
        {
            return _connector.GetRegisteredDatasetMessageSchema(_deviceName, _inboundEndpointName, _assetName, datasetName);
        }

        public MessageSchemaReference? GetRegisteredEventMessageSchema(string eventGroupName, string eventName)
        {
            return _connector.GetRegisteredEventMessageSchema(_deviceName, _inboundEndpointName, _assetName, eventGroupName, eventName);
        }

        /// <summary>
        /// Report a batch of datasets' runtime healths.
        /// </summary>
        /// <param name="runtimeHealth">The runtime healths of some datasets.</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry if any telemetry is sent.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This method uses the <see cref="AssetRuntimeHealthReporter"/> class, so it will de-duplicate runtime healths and will periodically report the last known
        /// runtime health for as long as this asset is available. Because of that, connector applications can freely call this method repeatedly even if the runtime health hasn't changed.
        /// </remarks>
        public async Task ReportDatasetRuntimeHealthAsync(List<ConnectorDatasetsRuntimeHealthEvent> runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            List<DatasetsRuntimeHealthEvent> servicesRuntimeHealths = new();
            foreach (ConnectorDatasetsRuntimeHealthEvent connectorRuntimeHealth in runtimeHealth)
            {
                servicesRuntimeHealths.Add(new DatasetsRuntimeHealthEvent()
                {
                    DatasetName = connectorRuntimeHealth.DatasetName,
                    RuntimeHealth = new()
                    {
                        LastUpdateTime = DateTime.UtcNow,
                        Message = connectorRuntimeHealth.RuntimeHealth.Message,
                        ReasonCode = connectorRuntimeHealth.RuntimeHealth.ReasonCode,
                        Status = connectorRuntimeHealth.RuntimeHealth.Status,
                        Version = _asset.Version ?? 0
                    }
                });
            }

            await _healthReporter.ReportDatasetHealthStatusAsync(servicesRuntimeHealths, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report a dataset's runtime health.
        /// </summary>
        /// <param name="datasetName">The name of the dataset</param>
        /// <param name="runtimeHealth">The runtime health of this dataset.</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry if any telemetry is sent.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This method uses the <see cref="AssetRuntimeHealthReporter"/> class, so it will de-duplicate runtime healths and will periodically report the last known
        /// runtime health for as long as this asset is available. Because of that, connector applications can freely call this method repeatedly even if the runtime health hasn't changed.
        /// </remarks>
        public async Task ReportDatasetRuntimeHealthAsync(string datasetName, ConnectorRuntimeHealth runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            var datasetsRuntimeHealthEvent = new ConnectorDatasetsRuntimeHealthEvent()
            {
                DatasetName = datasetName,
                RuntimeHealth = runtimeHealth,
            };

            await ReportDatasetRuntimeHealthAsync(new List<ConnectorDatasetsRuntimeHealthEvent>() { datasetsRuntimeHealthEvent }, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report a batch of events' runtime healths.
        /// </summary>
        /// <param name="runtimeHealth">The runtime healths of some events.</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry if any telemetry is sent.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This method uses the <see cref="AssetRuntimeHealthReporter"/> class, so it will de-duplicate runtime healths and will periodically report the last known
        /// runtime health for as long as this asset is available. Because of that, connector applications can freely call this method repeatedly even if the runtime health hasn't changed.
        /// </remarks>
        public async Task ReportEventRuntimeHealthAsync(List<ConnectorEventsRuntimeHealthEvent> runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            List<EventsRuntimeHealthEvent> servicesRuntimeHealths = new();
            foreach (ConnectorEventsRuntimeHealthEvent connectorRuntimeHealth in runtimeHealth)
            {
                servicesRuntimeHealths.Add(new EventsRuntimeHealthEvent()
                {
                    EventGroupName = connectorRuntimeHealth.EventGroupName,
                    EventName = connectorRuntimeHealth.EventName,
                    RuntimeHealth = new()
                    {
                        LastUpdateTime = DateTime.UtcNow,
                        Message = connectorRuntimeHealth.RuntimeHealth.Message,
                        ReasonCode = connectorRuntimeHealth.RuntimeHealth.ReasonCode,
                        Status = connectorRuntimeHealth.RuntimeHealth.Status,
                        Version = _asset.Version ?? 0
                    }
                });
            }
            await _healthReporter.ReportEventHealthStatusAsync(servicesRuntimeHealths, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report an event's runtime health.
        /// </summary>
        /// <param name="eventGroupName">The name of the event group that this event belongs to</param>
        /// <param name="eventName">The name of the event</param>
        /// <param name="runtimeHealth">The runtime health of this event.</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry if any telemetry is sent.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This method uses the <see cref="AssetRuntimeHealthReporter"/> class, so it will de-duplicate runtime healths and will periodically report the last known
        /// runtime health for as long as this asset is available. Because of that, connector applications can freely call this method repeatedly even if the runtime health hasn't changed.
        /// </remarks>
        public async Task ReportEventRuntimeHealthAsync(string eventGroupName, string eventName, ConnectorRuntimeHealth runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            ConnectorEventsRuntimeHealthEvent eventsRuntimeHealthEvent = new ConnectorEventsRuntimeHealthEvent()
            {
                EventGroupName = eventGroupName,
                EventName = eventName,
                RuntimeHealth = runtimeHealth,
            };

            //TODO need to add some caching at this layer such that not every report is sent (when nothing has changed) prior to
            //actually releasing this feature.
            await ReportEventRuntimeHealthAsync(new List<ConnectorEventsRuntimeHealthEvent>() { eventsRuntimeHealthEvent }, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report a batch of streams' runtime healths.
        /// </summary>
        /// <param name="runtimeHealth">The runtime healths of some streams.</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry if any telemetry is sent.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This method uses the <see cref="AssetRuntimeHealthReporter"/> class, so it will de-duplicate runtime healths and will periodically report the last known
        /// runtime health for as long as this asset is available. Because of that, connector applications can freely call this method repeatedly even if the runtime health hasn't changed.
        /// </remarks>
        public async Task ReportStreamRuntimeHealthAsync(List<ConnectorStreamsRuntimeHealthEvent> runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            List<StreamsRuntimeHealthEvent> servicesRuntimeHealths = new();
            foreach (ConnectorStreamsRuntimeHealthEvent connectorRuntimeHealth in runtimeHealth)
            {
                servicesRuntimeHealths.Add(new StreamsRuntimeHealthEvent()
                {
                    StreamName = connectorRuntimeHealth.StreamName,
                    RuntimeHealth = new()
                    {
                        LastUpdateTime = DateTime.UtcNow,
                        Message = connectorRuntimeHealth.RuntimeHealth.Message,
                        ReasonCode = connectorRuntimeHealth.RuntimeHealth.ReasonCode,
                        Status = connectorRuntimeHealth.RuntimeHealth.Status,
                        Version = _asset.Version ?? 0
                    }
                });
            }

            await _healthReporter.ReportStreamHealthStatusAsync(servicesRuntimeHealths, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report a stream's runtime health.
        /// </summary>
        /// <param name="streamName">The name of the stream</param>
        /// <param name="runtimeHealth">The runtime health of this stream.</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry if any telemetry is sent.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This method uses the <see cref="AssetRuntimeHealthReporter"/> class, so it will de-duplicate runtime healths and will periodically report the last known
        /// runtime health for as long as this asset is available. Because of that, connector applications can freely call this method repeatedly even if the runtime health hasn't changed.
        /// </remarks>
        public async Task ReportStreamRuntimeHealthAsync(string streamName, ConnectorRuntimeHealth runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            ConnectorStreamsRuntimeHealthEvent streamsRuntimeHealthEvent = new()
            {
                StreamName = streamName,
                RuntimeHealth = runtimeHealth,
            };

            await ReportStreamRuntimeHealthAsync(new List<ConnectorStreamsRuntimeHealthEvent>() { streamsRuntimeHealthEvent }, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report a batch of management actions' runtime healths.
        /// </summary>
        /// <param name="runtimeHealth">The runtime healths of some management actions.</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry if any telemetry is sent.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This method uses the <see cref="AssetRuntimeHealthReporter"/> class, so it will de-duplicate runtime healths and will periodically report the last known
        /// runtime health for as long as this asset is available. Because of that, connector applications can freely call this method repeatedly even if the runtime health hasn't changed.
        /// </remarks>
        public async Task ReportManagementActionRuntimeHealthAsync(List<ConnectorManagementActionsRuntimeHealthEvent> runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            List<ManagementActionsRuntimeHealthEvent> servicesRuntimeHealths = new();
            foreach (ConnectorManagementActionsRuntimeHealthEvent connectorRuntimeHealth in runtimeHealth)
            {
                servicesRuntimeHealths.Add(new ManagementActionsRuntimeHealthEvent()
                {
                    ManagementGroupName = connectorRuntimeHealth.ManagementGroupName,
                    ManagementActionName = connectorRuntimeHealth.ManagementActionName,
                    RuntimeHealth = new()
                    {
                        LastUpdateTime = DateTime.UtcNow,
                        Message = connectorRuntimeHealth.RuntimeHealth.Message,
                        ReasonCode = connectorRuntimeHealth.RuntimeHealth.ReasonCode,
                        Status = connectorRuntimeHealth.RuntimeHealth.Status,
                        Version = _asset.Version ?? 0
                    }
                });
            }

            await _healthReporter.ReportManagementActionHealthStatusAsync(servicesRuntimeHealths, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report a management action's runtime health.
        /// </summary>
        /// <param name="managementGroupName">The name of the management group that this action belongs to</param>
        /// <param name="managementActionName">The name of the management action</param>
        /// <param name="runtimeHealth">The runtime health of this management action.</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry if any telemetry is sent.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This method uses the <see cref="AssetRuntimeHealthReporter"/> class, so it will de-duplicate runtime healths and will periodically report the last known
        /// runtime health for as long as this asset is available. Because of that, connector applications can freely call this method repeatedly even if the runtime health hasn't changed.
        /// </remarks>
        public async Task ReportManagementActionRuntimeHealthAsync(string managementGroupName, string managementActionName, ConnectorRuntimeHealth runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            ConnectorManagementActionsRuntimeHealthEvent managementActionsRuntimeHealthEvent = new()
            {
                ManagementGroupName = managementGroupName,
                ManagementActionName= managementActionName,
                RuntimeHealth = runtimeHealth,
            };

            await ReportManagementActionRuntimeHealthAsync(new List<ConnectorManagementActionsRuntimeHealthEvent>() { managementActionsRuntimeHealthEvent }, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Change the interval at which this client will send background reports of the latest cached asset runtime healths
        /// </summary>
        /// <param name="reportingInterval">The new reporting interval</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <remarks>
        /// If background reporting is currently in progress, it will be cancelled and restarted with this new interval. If background reporting is not currently in progress,
        /// calling this method will not start it.
        /// </remarks>
        public async Task SetRuntimeHealthBackgroundReportingIntervalAsync(TimeSpan reportingInterval, CancellationToken cancellationToken = default)
        {
            await _healthReporter.SetRuntimeHealthBackgroundReportingIntervalAsync(reportingInterval, cancellationToken);
        }

        /// <summary>
        /// Update the status of this asset in the Azure Device Registry service
        /// </summary>
        /// <param name="status">The status of this asset and its datasets/event groups/streams/management groups</param>
        /// <param name="commandTimeout">The timeout for this RPC command invocation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The status returned by the Azure Device Registry service</returns>
        /// <remarks>
        /// This update behaves like a 'put' in that it will replace all current state for this asset in the Azure
        /// Device Registry service with what is provided.
        /// </remarks>
        private async Task<AssetStatus> UpdateAssetStatusAsync(
            AssetStatus status,
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return await _adrClient.UpdateAssetStatusAsync(
                _deviceName,
                _inboundEndpointName,
                new UpdateAssetStatusRequest()
                {
                    AssetName = _assetName,
                    AssetStatus = status,
                },
                commandTimeout,
                cancellationToken);
        }

        /// <summary>
        /// Get the status of this asset from the Azure Device Registry service
        /// </summary>
        /// <param name="commandTimeout">The timeout for this RPC command invocation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The status returned by the Azure Device Registry service</returns>
        private async Task<AssetStatus> GetAssetStatusAsync(
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return await _adrClient.GetAssetStatusAsync(
                _deviceName,
                _inboundEndpointName,
                _assetName,
                commandTimeout,
                cancellationToken);
        }

        // ============================================================
        // Management Action API
        // ============================================================

        /// <summary>
        /// Snapshot of the current asset revision. Replaced atomically by
        /// <see cref="ApplyAssetUpdateAsync"/>. Exposed only for <see cref="ManagementActionOrchestrator"/>,
        /// which enumerates the latest set of management actions after an update.
        /// </summary>
        internal Asset CurrentAsset => _asset;

        /// <summary>
        /// Apply an updated asset definition: diffs the new asset against the cached one, pushes per-action
        /// lifecycle notifications, swaps cached executors when an action's request topic changes, and signals
        /// <see cref="WaitForAssetUpdateAsync"/> so the orchestrator can spawn loops for newly-added actions.
        /// The AssetClient survives the update so in-flight handler state is preserved.
        /// </summary>
        /// <remarks>
        /// Notification rules per tracked (group, action):
        /// <list type="bullet">
        /// <item>Action gone → <see cref="ManagementActionDeleted"/>, stop+dispose cached executor.</item>
        /// <item>Request topic changed → build replacement executor, swap into cache, push
        /// <see cref="ManagementActionUpdatedWithNewExecutor"/>. Caller disposes the previous executor.</item>
        /// <item>Definition changed, topic unchanged → <see cref="ManagementActionUpdated"/>.</item>
        /// <item>Definition byte-identical → <see cref="ManagementActionAssetUpdated"/> so the loop can
        /// re-evaluate surrounding asset context (e.g. group defaults).</item>
        /// </list>
        /// Newly-introduced actions are surfaced via <see cref="WaitForAssetUpdateAsync"/> (no reader yet).
        /// </remarks>
        internal async Task ApplyAssetUpdateAsync(Asset newAsset, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(newAsset);

            await _assetUpdateMutex.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Asset oldAsset = _asset;
                _asset = newAsset;

                foreach (KeyValuePair<(string Group, string Action), ManagementActionState> kvp in _managementActionStates)
                {
                    (string groupName, string actionName) = kvp.Key;
                    ManagementActionState state = kvp.Value;

                    AssetManagementGroupAction? oldAction = FindAction(oldAsset, groupName, actionName);
                    AssetManagementGroup? newGroup = newAsset.ManagementGroups?
                        .FirstOrDefault(g => string.Equals(g.Name, groupName, StringComparison.Ordinal));
                    AssetManagementGroupAction? newAction = newGroup?.Actions?
                        .FirstOrDefault(a => string.Equals(a.Name, actionName, StringComparison.Ordinal));

                    if (newAction is null)
                    {
                        // Deleted from the asset (or its parent group went away).
                        await state.Mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
                        try
                        {
                            ManagementActionExecutor? cached = state.CurrentExecutor;
                            state.CurrentExecutor = null;
                            if (cached is not null)
                            {
                                try { await cached.StopAsync(cancellationToken).ConfigureAwait(false); }
                                catch { /* best-effort */ }
                                try { await cached.DisposeAsync().ConfigureAwait(false); }
                                catch { /* best-effort */ }
                            }
                        }
                        finally
                        {
                            state.Mutex.Release();
                        }
                        state.Notifications.Writer.TryWrite(new ManagementActionDeleted());
                        continue;
                    }

                    string? oldTopic = oldAction?.Topic;
                    string? newTopic = newAction.Topic;
                    bool topicChanged = !string.Equals(oldTopic, newTopic, StringComparison.Ordinal);

                    if (topicChanged)
                    {
                        ManagementActionExecutor? newExecutor;
                        await state.Mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
                        try
                        {
                            // Don't stop/dispose the previous executor here: the orchestrator holds the
                            // live reference and tears it down on ManagementActionUpdatedWithNewExecutor.
                            newExecutor = await BuildAndStartExecutorAsync(groupName, actionName, cancellationToken).ConfigureAwait(false);
                            state.CurrentExecutor = newExecutor;
                        }
                        finally
                        {
                            state.Mutex.Release();
                        }
                        state.Notifications.Writer.TryWrite(new ManagementActionUpdatedWithNewExecutor(newExecutor, Error: null));
                    }
                    else if (!ActionDefinitionEquals(oldAction, newAction))
                    {
                        state.Notifications.Writer.TryWrite(new ManagementActionUpdated(Error: null));
                    }
                    else
                    {
                        // Action unchanged but surrounding asset metadata may have changed (e.g. group
                        // defaults). Surface as AssetUpdated so the loop re-evaluates without claiming
                        // the action itself changed.
                        state.Notifications.Writer.TryWrite(new ManagementActionAssetUpdated(Error: null));
                    }
                }

                // Signal the orchestrator to pick up newly-added actions (not yet in
                // _managementActionStates). Existing loops are driven by their per-action channels above.
                _assetUpdatedSignal.Writer.TryWrite(newAsset);
            }
            finally
            {
                _assetUpdateMutex.Release();
            }
        }

        /// <summary>
        /// Await the next asset-update signal. Used by <see cref="ManagementActionOrchestrator"/> to discover
        /// newly-added management actions after an <see cref="ApplyAssetUpdateAsync"/> call.
        /// </summary>
        internal Task<Asset> WaitForAssetUpdateAsync(CancellationToken cancellationToken = default)
            => _assetUpdatedSignal.Reader.ReadAsync(cancellationToken).AsTask();

        private static AssetManagementGroupAction? FindAction(Asset asset, string groupName, string actionName)
            => asset.ManagementGroups?
                .FirstOrDefault(g => string.Equals(g.Name, groupName, StringComparison.Ordinal))?
                .Actions?
                .FirstOrDefault(a => string.Equals(a.Name, actionName, StringComparison.Ordinal));

        private static bool ActionDefinitionEquals(AssetManagementGroupAction? a, AssetManagementGroupAction? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return string.Equals(a.Name, b.Name, StringComparison.Ordinal)
                && string.Equals(a.Topic, b.Topic, StringComparison.Ordinal)
                && a.ActionType == b.ActionType
                && string.Equals(a.TargetUri, b.TargetUri, StringComparison.Ordinal)
                && string.Equals(a.TypeRef, b.TypeRef, StringComparison.Ordinal)
                && a.TimeoutInSeconds == b.TimeoutInSeconds
                && string.Equals(a.ActionConfiguration, b.ActionConfiguration, StringComparison.Ordinal);
        }

        /// <summary>
        /// Get the <see cref="ManagementActionExecutor"/> for the given group/action, or <c>null</c> if no
        /// valid executor exists right now (e.g. the current definition was rejected, or the action is still
        /// initializing). A null result is not an error; callers should await
        /// <see cref="RecvManagementActionNotificationAsync"/> for the next definition and retry.
        /// </summary>
        internal async Task<ManagementActionExecutor?> GetManagementActionExecutorAsync(
            string managementGroupName,
            string managementActionName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(managementGroupName);
            ArgumentException.ThrowIfNullOrEmpty(managementActionName);

            ManagementActionState state = _managementActionStates.GetOrAdd(
                (managementGroupName, managementActionName),
                _ => new ManagementActionState());

            await state.Mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (state.CurrentExecutor is not null)
                {
                    return state.CurrentExecutor;
                }

                ManagementActionExecutor? executor = await BuildAndStartExecutorAsync(
                    managementGroupName,
                    managementActionName,
                    cancellationToken).ConfigureAwait(false);

                state.CurrentExecutor = executor;
                return executor;
            }
            finally
            {
                state.Mutex.Release();
            }
        }

        /// <summary>
        /// Await the next lifecycle notification for <paramref name="managementGroupName"/> /
        /// <paramref name="managementActionName"/>. Exits (returns
        /// <see cref="ManagementActionDeleted"/>) when the action is removed from the asset
        /// definition or the asset itself is deleted.
        /// </summary>
        internal Task<ManagementActionNotification> RecvManagementActionNotificationAsync(
            string managementGroupName,
            string managementActionName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(managementGroupName);
            ArgumentException.ThrowIfNullOrEmpty(managementActionName);

            ManagementActionState state = _managementActionStates.GetOrAdd(
                (managementGroupName, managementActionName),
                _ => new ManagementActionState());

            return state.Notifications.Reader.ReadAsync(cancellationToken).AsTask();
        }

        /// <summary>
        /// Pause periodic runtime-health reporting for a management action so the next health event
        /// reflects the re-validated definition.
        /// </summary>
        internal async Task PauseManagementActionRuntimeHealthReportingAsync(
            string managementGroupName,
            string managementActionName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(managementGroupName);
            ArgumentException.ThrowIfNullOrEmpty(managementActionName);
            cancellationToken.ThrowIfCancellationRequested();

            // Stop emissions until the next ReportManagementActionRuntimeHealthAsync sets a fresh status;
            // ADR then lapses to Unknown rather than re-asserting a stale one. The reporter owns the pause state.
            await _healthReporter.PauseReportingManagementActionAsync(managementGroupName, managementActionName, cancellationToken);
        }

        /// <summary>
        /// Build and start a <see cref="ManagementActionExecutor"/> for the given group/action from the
        /// cached asset definition. Returns <c>null</c> when the action is unknown or its request topic is
        /// missing/empty &mdash; callers should surface that as a config error, not a fault. The request
        /// topic is taken verbatim from <see cref="AssetManagementGroupAction.Topic"/>.
        /// </summary>
        private async Task<ManagementActionExecutor?> BuildAndStartExecutorAsync(
            string managementGroupName,
            string managementActionName,
            CancellationToken cancellationToken)
        {
            AssetManagementGroup? group = _asset.ManagementGroups?
                .FirstOrDefault(g => string.Equals(g.Name, managementGroupName, StringComparison.Ordinal));
            AssetManagementGroupAction? action = group?.Actions?
                .FirstOrDefault(a => string.Equals(a.Name, managementActionName, StringComparison.Ordinal));

            if (group is null || action is null)
            {
                return null;
            }

            string? requestTopic = action.Topic;
            if (string.IsNullOrEmpty(requestTopic))
            {
                return null;
            }

            ulong? timeoutSeconds = action.TimeoutInSeconds ?? group.DefaultTimeoutInSeconds;
            TimeSpan executionTimeout = timeoutSeconds is { } secs && secs > 0
                ? TimeSpan.FromSeconds(secs)
                : DefaultManagementActionExecutionTimeout;

            ManagementActionExecutor executor = new(
                _connector.ApplicationContext,
                _connector.MqttPubSubClient,
                _deviceName,
                _assetName,
                managementGroupName,
                managementActionName,
                action.ActionType,
                requestTopic,
                serviceGroupId: string.Empty,
                executionTimeout,
                topicTokenMap: new Dictionary<string, string>());

            try
            {
                await executor.StartAsync(cancellationToken).ConfigureAwait(false);
                return executor;
            }
            catch
            {
                await executor.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private static readonly TimeSpan DefaultManagementActionExecutionTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Per-(group, action) state for the management-action API. One instance per distinct action.
        /// </summary>
        private sealed class ManagementActionState
        {
            public SemaphoreSlim Mutex { get; } = new(1, 1);
            public Channel<ManagementActionNotification> Notifications { get; } =
                Channel.CreateUnbounded<ManagementActionNotification>(
                    new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            public ManagementActionExecutor? CurrentExecutor { get; set; }
        }

        // ============================================================
        // End of Management Action API
        // ============================================================

        public virtual async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            GC.SuppressFinalize(this);
        }

        public virtual async ValueTask DisposeAsync(bool disposing)
        {
            await DisposeAsyncCore();
        }

        private async ValueTask DisposeAsyncCore()
        {
            // Stop and dispose any management-action executors we created on demand, and
            // signal Deleted to any orchestrator currently parked in RecvManagementActionNotificationAsync
            // so it can exit its per-action loop cleanly.
            foreach (KeyValuePair<(string Group, string Action), ManagementActionState> kvp in _managementActionStates)
            {
                ManagementActionState state = kvp.Value;
                ManagementActionExecutor? executor = state.CurrentExecutor;
                state.CurrentExecutor = null;
                if (executor is not null)
                {
                    try
                    {
                        await executor.StopAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // Best-effort during dispose.
                    }

                    try
                    {
                        await executor.DisposeAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // Best-effort during dispose.
                    }
                }

                state.Notifications.Writer.TryWrite(new ManagementActionDeleted());
                state.Notifications.Writer.TryComplete();

                try
                {
                    state.Mutex.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Best-effort during dispose.
                }
            }

            _managementActionStates.Clear();

            _assetUpdatedSignal.Writer.TryComplete();

            try
            {
                _assetUpdateMutex.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // It's fine if this semaphore is already disposed.
            }

            try
            {
                _statusUpdateMutex.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // It's fine if this semaphore is already disposed.
            }

            try
            {
                await _healthReporter.CancelHealthStatusReportingAsync();
                await _healthReporter.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
                // It's fine if this is already disposed.
            }
        }
    }
}
