// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry
{
    /// <summary>
    /// A class for smartly sending runtime health events for a specific asset to the Azure Device Registry service
    /// </summary>
    /// <remarks>
    /// This class has two main features that differentiate it from just directly calling the runtime health update APIs like <see cref="IAzureDeviceRegistryClient.ReportDatasetRuntimeHealthAsync(string, string, string, List{DatasetsRuntimeHealthEvent}, TimeSpan?, CancellationToken)"/>:
    ///  1) De-duplication of dataset/stream/event/management action runtime healths. This allows you to write your connector code such that it repeatedly calls an API like <see cref="ReportDatasetHealthStatusAsync(List{DatasetsRuntimeHealthEvent}, TimeSpan?, CancellationToken)"/>
    ///  even if the runtime health has not changed as this client will cache the last sent runtime health and check if the new runtime health actually needs to be forwarded to the service.
    ///  2) Periodic reporting of the last known runtime healths of the datasets/streams/events/management actions with updated timestamps. Connectors are advised to periodically send these updates to ensure that
    ///  the Azure Device Registry service has an up-to-date picture of the runtime health of each dataset/stream/event/management action and this class handles that for you. Additionally, the periodic updates can
    ///  be paused for specific datasets/streams/events/management actions when their runtime health becomes unknown. This background reporting will send the runtime health for all non-paused datasets/streams/events/management actions
    ///  at once and the period that it does this can be changed with <see cref="SetRuntimeHealthBackgroundReportingIntervalAsync(TimeSpan, CancellationToken)"/>.
    /// </remarks>
    public class AssetRuntimeHealthReporter : IAsyncDisposable
    {
        // Any null RuntimeHealth as a value in the below dictionaries signals that reporting is paused for this dataset/stream/etc or that no runtime health has been reported by the user yet.
        private readonly Dictionary<string, RuntimeHealth?> _cachedDatasetsRuntimeHealth = new(); // keys are dataset names, values are their corresponding cached healths
        private readonly Dictionary<string, RuntimeHealth?> _cachedStreamsRuntimeHealth = new(); // keys are stream names, values are their corresponding cached healths
        private readonly Dictionary<string, Dictionary<string, RuntimeHealth?>> _cachedEventGroupsRuntimeHealth = new(); // keys are event group names, values are dictionaries where keys are event names and the values are the cached health of that event group's event
        private readonly Dictionary<string, Dictionary<string, RuntimeHealth?>> _cachedManagementGroupsRuntimeHealth = new(); // keys are management group names, values are dictionaries where keys are management action names and the values are the cached health of that management group's action

        private readonly SemaphoreSlim _concurrentModificationLock = new SemaphoreSlim(1, 1);

        private Countdown _periodicSender;

        private readonly IAzureDeviceRegistryClient _azureDeviceRegistryClient;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;
        private readonly string _assetName;

        /// <summary>
        /// Create a new runtime health reporter for a given asset
        /// </summary>
        /// <param name="azureDeviceRegistryClient">The Azure Device Registry client used to send these runtime health events.</param>
        /// <param name="deviceName">The name of the device.</param>
        /// <param name="inboundEndpointName">The name of the inbound endpoint</param>
        /// <param name="assetName">The name of the asset.</param>
        /// <param name="reportingPeriod">The interval at which to send background reports.</param>
        /// <remarks>
        /// Background reporting does not start until the first runtime health of a dataset/stream/event/management action is set by calling a method like <see cref="ReportDatasetHealthStatusAsync(string, RuntimeHealth, TimeSpan?, CancellationToken)"/>.
        /// </remarks>
        public AssetRuntimeHealthReporter(IAzureDeviceRegistryClient azureDeviceRegistryClient, string deviceName, string inboundEndpointName, string assetName, TimeSpan? reportingPeriod = null)
        {
            _azureDeviceRegistryClient = azureDeviceRegistryClient;
            _deviceName = deviceName;
            _inboundEndpointName = inboundEndpointName;
            _assetName = assetName;
            _periodicSender = new Countdown(reportingPeriod ?? TimeSpan.FromSeconds(10), SendPeriodicReportAsync);
        }

        /// <summary>
        /// Pause background reporting until a new health status is set for this dataset
        /// </summary>
        /// <param name="datasetName">The dataset to pause health status reporting for.</param>
        /// <remarks>
        /// This is used to signal that the last known health status may no longer be applicable. This does not affect background reporting of other datasets/streams/events/management actions that were already active.
        /// </remarks>
        public async Task PauseReportingDatasetAsync(string datasetName, CancellationToken cancellationToken = default)
        {
            await _concurrentModificationLock.WaitAsync(cancellationToken);
            try
            {
                _cachedDatasetsRuntimeHealth[datasetName] = null;
            }
            finally
            {
                _concurrentModificationLock.Release();
            }
        }

        /// <summary>
        /// Pause background reporting until a new health status is set for this stream
        /// </summary>
        /// <param name="streamName">The stream to pause health status reporting for.</param>
        /// <remarks>
        /// This is used to signal that the last known health status may no longer be applicable. This does not affect background reporting of other datasets/streams/events/management actions that were already active.
        /// </remarks>
        public async Task PauseReportingStreamAsync(string streamName, CancellationToken cancellationToken = default)
        {
            await _concurrentModificationLock.WaitAsync(cancellationToken);
            try
            {
                _cachedStreamsRuntimeHealth[streamName] = null;
            }
            finally
            {
                _concurrentModificationLock.Release();
            }
        }

        /// <summary>
        /// Pause background reporting until a new health status is set for this event
        /// </summary>
        /// <param name="eventGroupName">The event group whose event should pause reporting.</param>
        /// <param name="eventName">The event within the given event group that should pause reporting</param>
        /// <remarks>
        /// This is used to signal that the last known health status may no longer be applicable.
        /// </remarks>
        public async Task PauseReportingEventAsync(string eventGroupName, string eventName, CancellationToken cancellationToken = default)
        {
            await _concurrentModificationLock.WaitAsync(cancellationToken);
            try
            {
                if (_cachedEventGroupsRuntimeHealth.TryGetValue(eventGroupName, out Dictionary<string, RuntimeHealth?>? eventHealths))
                {
                    eventHealths[eventName] = null;
                }
            }
            finally
            {
                _concurrentModificationLock.Release();
            }
        }

        /// <summary>
        /// Pause background reporting until a new health status is set for this event
        /// </summary>
        /// <param name="managementGroupName">The management group whose action should pause reporting.</param>
        /// <param name="managementActionName">The action within the given management group that should pause reporting</param>
        /// <remarks>
        /// This is used to signal that the last known health status may no longer be applicable. This does not affect background reporting of other datasets/streams/events/management actions that were already active.
        /// </remarks>
        public async Task PauseReportingManagementActionAsync(string managementGroupName, string managementActionName, CancellationToken cancellationToken = default)
        {
            await _concurrentModificationLock.WaitAsync(cancellationToken);
            try
            {
                if (_cachedManagementGroupsRuntimeHealth.TryGetValue(managementGroupName, out Dictionary<string, RuntimeHealth?>? actionHealths))
                {
                    actionHealths[managementActionName] = null;
                }
            }
            finally
            {
                _concurrentModificationLock.Release();
            }
        }

        /// <summary>
        /// Stop all background reporting.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>Generally, this method should only be called if the asset this client reports for is no longer available.</remarks>
        public async Task CancelHealthStatusReportingAsync(CancellationToken cancellationToken = default)
        {
            await _periodicSender.StopAsync(cancellationToken);
        }

        /// <summary>
        /// Report the runtime health of a single dataset if it is worth reporting.
        /// </summary>
        /// <param name="datasetName">The name of the dataset</param>
        /// <param name="datasetRuntimeHealth">The runtime health of that dataset</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry (if any telemetry is sent).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// If background reporting has not started for this dataset, or it was paused, then background reporting will start/resume. If this runtime health is functionally identical to the previously reported
        /// runtime health (identical other than timestamp), then no telemetry will be sent to the Azure Device Registry service.
        /// </remarks>
        public async Task ReportDatasetHealthStatusAsync(string datasetName, RuntimeHealth datasetRuntimeHealth, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            var datasetHealthEvent = new DatasetsRuntimeHealthEvent()
            {
                DatasetName = datasetName,
                RuntimeHealth = datasetRuntimeHealth,
            };

            await ReportDatasetHealthStatusAsync(new List<DatasetsRuntimeHealthEvent> { datasetHealthEvent }, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report a batch of runtime healths of datasets if any of them are worth reporting.
        /// </summary>
        /// <param name="datasetRuntimeHealths">The batch of dataset runtime healths.</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry (if any telemetry is sent).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// If background reporting has not started for any of these datasets, or it was paused, then background reporting will start/resume. This method will only send the runtime healths in this batch
        /// that are not functionally identical to their previously reported runtime health (identical other than timestamp). If none of these runtime healths are worth reporting, then no telemetry will be sent.
        /// </remarks>
        public async Task ReportDatasetHealthStatusAsync(List<DatasetsRuntimeHealthEvent> datasetRuntimeHealths, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            List<DatasetsRuntimeHealthEvent> eventsToReport = new();

            await _concurrentModificationLock.WaitAsync(cancellationToken);
            try
            {
                foreach (DatasetsRuntimeHealthEvent datasetRuntimeHealthEvent in datasetRuntimeHealths)
                {
                    string datasetName = datasetRuntimeHealthEvent.DatasetName;
                    RuntimeHealth datasetRuntimeHealth = datasetRuntimeHealthEvent.RuntimeHealth;
                    _cachedDatasetsRuntimeHealth.TryGetValue(datasetName, out RuntimeHealth? cachedHealth);
                    RuntimeHealth.CompareNewHealthWithCachedHealth(datasetRuntimeHealth, cachedHealth, out bool updateCache, out bool sendIt);

                    if (updateCache)
                    {
                        _cachedDatasetsRuntimeHealth[datasetName] = datasetRuntimeHealth;
                    }

                    if (sendIt)
                    {
                        // Send it in bulk with the other reportable events down below
                        eventsToReport.Add(datasetRuntimeHealthEvent);
                    }

                    if ((updateCache || sendIt) && !_periodicSender.IsRunning())
                    {
                        await _periodicSender.StartAsync(cancellationToken);
                    }
                }
            }
            finally
            {
                _concurrentModificationLock.Release();
            }

            if (eventsToReport.Count > 0)
            {
                await _azureDeviceRegistryClient.ReportDatasetRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, eventsToReport, telemetryTimeout, cancellationToken);
            }
        }

        /// <summary>
        /// Report the runtime health of a single stream if it is worth reporting.
        /// </summary>
        /// <param name="streamName">The name of the stream</param>
        /// <param name="streamRuntimeHealth">The runtime health of that stream</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry (if any telemetry is sent).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// If background reporting has not started for this stream, or it was paused, then background reporting will start/resume. If this runtime health is functionally identical to the previously reported
        /// runtime health (identical other than timestamp), then no telemetry will be sent to the Azure Device Registry service.
        /// </remarks>
        public async Task ReportStreamHealthStatusAsync(string streamName, RuntimeHealth streamRuntimeHealth, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            var streamsHealthEvent = new StreamsRuntimeHealthEvent()
            {
                StreamName = streamName,
                RuntimeHealth = streamRuntimeHealth,
            };

            await ReportStreamHealthStatusAsync(new List<StreamsRuntimeHealthEvent> { streamsHealthEvent }, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report a batch of runtime healths of streams if any of them are worth reporting.
        /// </summary>
        /// <param name="streamRuntimeHealths">The batch of stream runtime healths.</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry (if any telemetry is sent).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// If background reporting has not started for any of these streams, or it was paused, then background reporting will start/resume. This method will only send the runtime healths in this batch
        /// that are not functionally identical to their previously reported runtime health (identical other than timestamp). If none of these runtime healths are worth reporting, then no telemetry will be sent.
        /// </remarks>
        public async Task ReportStreamHealthStatusAsync(List<StreamsRuntimeHealthEvent> streamRuntimeHealths, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            List<StreamsRuntimeHealthEvent> eventsToReport = new();

            await _concurrentModificationLock.WaitAsync(cancellationToken);
            try
            {
                foreach (StreamsRuntimeHealthEvent streamRuntimeHealthEvent in streamRuntimeHealths)
                {
                    string streamName = streamRuntimeHealthEvent.StreamName;
                    RuntimeHealth streamRuntimeHealth = streamRuntimeHealthEvent.RuntimeHealth;
                    _cachedStreamsRuntimeHealth.TryGetValue(streamName, out RuntimeHealth? cachedHealth);
                    RuntimeHealth.CompareNewHealthWithCachedHealth(streamRuntimeHealth, cachedHealth, out bool updateCache, out bool sendIt);

                    if (updateCache)
                    {
                        _cachedStreamsRuntimeHealth[streamName] = streamRuntimeHealth;
                    }

                    if (sendIt)
                    {
                        // Send it in bulk with the other reportable events down below
                        eventsToReport.Add(streamRuntimeHealthEvent);
                    }

                    if ((updateCache || sendIt) && !_periodicSender.IsRunning())
                    {
                        await _periodicSender.StartAsync(cancellationToken);
                    }
                }
            }
            finally
            {
                _concurrentModificationLock.Release();
            }

            if (eventsToReport.Count > 0)
            {
                await _azureDeviceRegistryClient.ReportStreamRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, eventsToReport, telemetryTimeout, cancellationToken);
            }
        }

        /// <summary>
        /// Report the runtime health of a single event if it is worth reporting.
        /// </summary>
        /// <param name="eventGroupName">The name of the event group that the event belongs to</param>
        /// <param name="eventName">The name of the event</param>
        /// <param name="eventRuntimeHealth">The runtime health of that event</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry (if any telemetry is sent).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// If background reporting has not started for this event, or it was paused, then background reporting will start/resume. If this runtime health is functionally identical to the previously reported
        /// runtime health (identical other than timestamp), then no telemetry will be sent to the Azure Device Registry service.
        /// </remarks>
        public async Task ReportEventHealthStatusAsync(string eventGroupName, string eventName, RuntimeHealth eventRuntimeHealth, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            var eventsHealthEvent = new EventsRuntimeHealthEvent()
            {
                EventGroupName = eventGroupName,
                EventName = eventName,
                RuntimeHealth = eventRuntimeHealth,
            };

            await ReportEventHealthStatusAsync(new List<EventsRuntimeHealthEvent> { eventsHealthEvent }, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report a batch of runtime healths of events if any of them are worth reporting.
        /// </summary>
        /// <param name="eventRuntimeHealths">The batch of event runtime healths.</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry (if any telemetry is sent).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// If background reporting has not started for any of these events, or it was paused, then background reporting will start/resume. This method will only send the runtime healths in this batch
        /// that are not functionally identical to their previously reported runtime health (identical other than timestamp). If none of these runtime healths are worth reporting, then no telemetry will be sent.
        /// </remarks>
        public async Task ReportEventHealthStatusAsync(List<EventsRuntimeHealthEvent> eventRuntimeHealths, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            List<EventsRuntimeHealthEvent> eventsToReport = new();

            await _concurrentModificationLock.WaitAsync(cancellationToken);
            try
            {
                foreach (EventsRuntimeHealthEvent eventsRuntimeHealthEvent in eventRuntimeHealths)
                {
                    string eventGroupName = eventsRuntimeHealthEvent.EventGroupName;
                    string eventName = eventsRuntimeHealthEvent.EventName;
                    RuntimeHealth runtimeHealth = eventsRuntimeHealthEvent.RuntimeHealth;
                    RuntimeHealth? cachedHealth = null;
                    if (_cachedEventGroupsRuntimeHealth.TryGetValue(eventGroupName, out Dictionary<string, RuntimeHealth?>? events))
                    {
                        events.TryGetValue(eventName, out cachedHealth);
                    }

                    RuntimeHealth.CompareNewHealthWithCachedHealth(runtimeHealth, cachedHealth, out bool updateCache, out bool sendIt);

                    if (updateCache)
                    {
                        if (!_cachedEventGroupsRuntimeHealth.ContainsKey(eventGroupName))
                        {
                            _cachedEventGroupsRuntimeHealth[eventGroupName] = new();
                        }

                        _cachedEventGroupsRuntimeHealth[eventGroupName][eventName] = runtimeHealth;
                    }

                    if (sendIt)
                    {
                        // Send it in bulk with the other reportable events down below
                        eventsToReport.Add(eventsRuntimeHealthEvent);
                    }

                    if ((updateCache || sendIt) && !_periodicSender.IsRunning())
                    {
                        await _periodicSender.StartAsync(cancellationToken);
                    }
                }
            }
            finally
            {
                _concurrentModificationLock.Release();
            }

            if (eventsToReport.Count > 0)
            {
                await _azureDeviceRegistryClient.ReportEventRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, eventsToReport, telemetryTimeout, cancellationToken);
            }
        }

        /// <summary>
        /// Report the runtime health of a single management action if it is worth reporting.
        /// </summary>
        /// <param name="managementGroupName">The name of the management group that the management action belongs to</param>
        /// <param name="managementActionName">The name of the event</param>
        /// <param name="managementActionRuntimeHealth">The runtime health of that management action</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry (if any telemetry is sent).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// If background reporting has not started for this management action, or it was paused, then background reporting will start/resume. If this runtime health is functionally identical to the previously reported
        /// runtime health (identical other than timestamp), then no telemetry will be sent to the Azure Device Registry service.
        /// </remarks>
        public async Task ReportManagementActionHealthStatusAsync(string managementGroupName, string managementActionName, RuntimeHealth managementActionRuntimeHealth, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            var managementActionsHealthEvent = new ManagementActionsRuntimeHealthEvent()
            {
                ManagementGroupName = managementGroupName,
                ManagementActionName = managementActionName,
                RuntimeHealth = managementActionRuntimeHealth,
            };

            await ReportManagementActionHealthStatusAsync(new List<ManagementActionsRuntimeHealthEvent> { managementActionsHealthEvent }, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report a batch of runtime healths of management actions if any of them are worth reporting.
        /// </summary>
        /// <param name="managementActionRuntimeHealths">The batch of management action runtime healths.</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry (if any telemetry is sent).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// If background reporting has not started for any of these management actions, or it was paused, then background reporting will start/resume. This method will only send the runtime healths in this batch
        /// that are not functionally identical to their previously reported runtime health (identical other than timestamp). If none of these runtime healths are worth reporting, then no telemetry will be sent.
        /// </remarks>
        public async Task ReportManagementActionHealthStatusAsync(List<ManagementActionsRuntimeHealthEvent> managementActionRuntimeHealths, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            List<ManagementActionsRuntimeHealthEvent> eventsToReport = new();

            await _concurrentModificationLock.WaitAsync(cancellationToken);
            try
            {
                foreach (ManagementActionsRuntimeHealthEvent managementActionsRuntimeHealthEvent in managementActionRuntimeHealths)
                {
                    string managementGroupName = managementActionsRuntimeHealthEvent.ManagementGroupName;
                    string managementActionName = managementActionsRuntimeHealthEvent.ManagementActionName;
                    RuntimeHealth runtimeHealth = managementActionsRuntimeHealthEvent.RuntimeHealth;
                    RuntimeHealth? cachedHealth = null;
                    if (_cachedManagementGroupsRuntimeHealth.TryGetValue(managementGroupName, out Dictionary<string, RuntimeHealth?>? actions))
                    {
                        actions.TryGetValue(managementActionName, out cachedHealth);
                    }

                    RuntimeHealth.CompareNewHealthWithCachedHealth(runtimeHealth, cachedHealth, out bool updateCache, out bool sendIt);

                    if (updateCache)
                    {
                        if (!_cachedManagementGroupsRuntimeHealth.ContainsKey(managementGroupName))
                        {
                            _cachedManagementGroupsRuntimeHealth[managementGroupName] = new();
                        }

                        _cachedManagementGroupsRuntimeHealth[managementGroupName][managementActionName] = runtimeHealth;
                    }

                    if (sendIt)
                    {
                        // Send it in bulk with the other reportable events down below
                        eventsToReport.Add(managementActionsRuntimeHealthEvent);
                    }

                    if ((updateCache || sendIt) && !_periodicSender.IsRunning())
                    {
                        await _periodicSender.StartAsync(cancellationToken);
                    }
                }
            }
            finally
            {
                _concurrentModificationLock.Release();
            }

            if (eventsToReport.Count > 0)
            {
                await _azureDeviceRegistryClient.ReportManagementActionRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, eventsToReport, telemetryTimeout, cancellationToken);
            }
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
            bool isCurrentlyRunning = _periodicSender.IsRunning();
            if (isCurrentlyRunning)
            {
                await _periodicSender.StopAsync(cancellationToken);
                _periodicSender.Dispose();
            }

            _periodicSender = new(reportingInterval, SendPeriodicReportAsync);

            if (isCurrentlyRunning)
            {
                await _periodicSender.StartAsync(cancellationToken);
            }
        }

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
            // wait for any in-progress background reporting to finish
            await _concurrentModificationLock.WaitAsync();

            try
            {
                await _periodicSender.StopAsync();
                _periodicSender.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // It's fine if this is already disposed.
            }

            try
            {
                _concurrentModificationLock.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // It's fine if this is already disposed.
            }
        }

        private async Task SendPeriodicReportAsync(CancellationToken cancellationToken)
        {
            await _concurrentModificationLock.WaitAsync(cancellationToken);
            try
            {
                // Report all the cached (and non-paused) dataset runtime healths in one message
                List<DatasetsRuntimeHealthEvent> datasetRuntimeHealthsToReport = new();
                foreach (string datasetName in _cachedDatasetsRuntimeHealth.Keys)
                {
                    if (_cachedDatasetsRuntimeHealth.TryGetValue(datasetName, out RuntimeHealth? cachedDatasetHealth) && cachedDatasetHealth != null)
                    {
                        datasetRuntimeHealthsToReport.Add(new DatasetsRuntimeHealthEvent()
                        {
                            DatasetName = datasetName,
                            RuntimeHealth = cachedDatasetHealth,
                        });
                    }
                }

                if (datasetRuntimeHealthsToReport.Count > 0)
                {
                    await _azureDeviceRegistryClient.ReportDatasetRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, datasetRuntimeHealthsToReport, cancellationToken: cancellationToken);
                }

                // Report all the cached (and non-paused) stream runtime healths in one message
                List<StreamsRuntimeHealthEvent> streamRuntimeHealthsToReport = new();
                foreach (string streamName in _cachedStreamsRuntimeHealth.Keys)
                {
                    if (_cachedStreamsRuntimeHealth.TryGetValue(streamName, out RuntimeHealth? cachedStreamHealth) && cachedStreamHealth != null)
                    {
                        streamRuntimeHealthsToReport.Add(new StreamsRuntimeHealthEvent()
                        {
                            StreamName = streamName,
                            RuntimeHealth = cachedStreamHealth,
                        });
                    }
                }

                if (streamRuntimeHealthsToReport.Count > 0)
                {
                    await _azureDeviceRegistryClient.ReportStreamRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, streamRuntimeHealthsToReport, cancellationToken: cancellationToken);
                }

                // Report all the cached (and non-paused) event runtime healths in one message
                List<EventsRuntimeHealthEvent> eventRuntimeHealthsToReport = new();
                foreach (string eventGroupName in _cachedEventGroupsRuntimeHealth.Keys)
                {
                    if (_cachedEventGroupsRuntimeHealth.TryGetValue(eventGroupName, out Dictionary<string, RuntimeHealth?>? cachedEventHealths))
                    {
                        foreach (string eventName in cachedEventHealths.Keys)
                        {
                            if (cachedEventHealths.TryGetValue(eventName, out RuntimeHealth? cachedEventHealth) && cachedEventHealth != null)
                            {
                                eventRuntimeHealthsToReport.Add(new EventsRuntimeHealthEvent()
                                {
                                    EventGroupName = eventGroupName,
                                    EventName = eventName,
                                    RuntimeHealth = cachedEventHealth,
                                });
                            }
                        }
                    }
                }

                if (eventRuntimeHealthsToReport.Count > 0)
                {
                    await _azureDeviceRegistryClient.ReportEventRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, eventRuntimeHealthsToReport, cancellationToken: cancellationToken);
                }

                // Report all the cached (and non-paused) management action runtime healths in one message
                List<ManagementActionsRuntimeHealthEvent> managementActionRuntimeHealthsToReport = new();
                foreach (string managementGroupName in _cachedManagementGroupsRuntimeHealth.Keys)
                {
                    if (_cachedManagementGroupsRuntimeHealth.TryGetValue(managementGroupName, out Dictionary<string, RuntimeHealth?>? cachedManagementActionHealths))
                    {
                        foreach (string managementActionName in cachedManagementActionHealths.Keys)
                        {
                            if (cachedManagementActionHealths.TryGetValue(managementActionName, out RuntimeHealth? cachedManagementActionHealth) && cachedManagementActionHealth != null)
                            {
                                managementActionRuntimeHealthsToReport.Add(new ManagementActionsRuntimeHealthEvent()
                                {
                                    ManagementGroupName = managementGroupName,
                                    ManagementActionName = managementActionName,
                                    RuntimeHealth = cachedManagementActionHealth,
                                });
                            }
                        }
                    }
                }

                if (managementActionRuntimeHealthsToReport.Count > 0)
                {
                    await _azureDeviceRegistryClient.ReportManagementActionRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, managementActionRuntimeHealthsToReport, cancellationToken: cancellationToken);
                }
            }
            finally
            {
                _concurrentModificationLock.Release();
            }
        }
    }
}
