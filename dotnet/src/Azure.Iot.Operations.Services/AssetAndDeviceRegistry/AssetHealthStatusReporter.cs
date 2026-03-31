// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry
{
    // allows user to send individual health events (if they are diff from cached) and also sends a batch of those cached health events periodically regardless.
    // users can still stop the periodic sending of particular events/streams/datasets/etc without preventing the other events/streams/datasets/etc from their
    // periodic batch send. The periodic sending starts as soon as the first entity reports a health status and stops if all cached entries have been paused or
    // if the reporter is disposed.
    public class AssetHealthStatusReporter : IAsyncDisposable
    {
        // Any null RuntimeHealth as a value in the below dictionaries signals that reporting is paused for this dataset/stream/etc or that no runtime health has been reported by the user yet.
        private readonly Dictionary<string, RuntimeHealth?> _cachedDatasetsRuntimeHealth = new(); // keys are dataset names, values are their corresponding cached healths
        private readonly Dictionary<string, RuntimeHealth?> _cachedStreamsRuntimeHealth = new(); // keys are stream names, values are their corresponding cached healths
        private readonly Dictionary<string, Dictionary<string, RuntimeHealth?>> _cachedEventGroupsRuntimeHealth = new(); // keys are event group names, values are dictionaries where keys are event names and the values are the cached health of that event group's event
        private readonly Dictionary<string, Dictionary<string, RuntimeHealth?>> _cachedManagementGroupsRuntimeHealth = new(); // keys are management group names, values are dictionaries where keys are management action names and the values are the cached health of that management group's action

        private readonly Countdown _periodicSender;

        private readonly IAzureDeviceRegistryClient _azureDeviceRegistryClient;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;
        private readonly string _assetName;

        public AssetHealthStatusReporter(IAzureDeviceRegistryClient azureDeviceRegistryClient, string deviceName, string inboundEndpointName, string assetName, TimeSpan? reportingPeriod = null)
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
        /// This is used to signal that the last known health status may no longer be applicable.
        /// </remarks>
        public void PauseReportingDataset(string datasetName)
        {
            _cachedDatasetsRuntimeHealth[datasetName] = null;
        }

        /// <summary>
        /// Pause background reporting until a new health status is set for this stream
        /// </summary>
        /// <param name="streamName">The stream to pause health status reporting for.</param>
        /// <remarks>
        /// This is used to signal that the last known health status may no longer be applicable.
        /// </remarks>
        public void PauseReportingStream(string streamName)
        {
            _cachedStreamsRuntimeHealth[streamName] = null;
        }

        /// <summary>
        /// Pause background reporting until a new health status is set for this event
        /// </summary>
        /// <param name="eventGroupName">The event group whose event should pause reporting.</param>
        /// <param name="eventName">The event within the given event group that should pause reporting</param>
        /// <remarks>
        /// This is used to signal that the last known health status may no longer be applicable.
        /// </remarks>
        public void PauseReportingEvent(string eventGroupName, string eventName)
        {
            if (_cachedEventGroupsRuntimeHealth.TryGetValue(eventGroupName, out Dictionary<string, RuntimeHealth?>? eventHealths))
            {
                eventHealths[eventName] = null;
            }
        }

        /// <summary>
        /// Pause background reporting until a new health status is set for this event
        /// </summary>
        /// <param name="managementGroupName">The management group whose action should pause reporting.</param>
        /// <param name="managementActionName">The action within the given management group that should pause reporting</param>
        /// <remarks>
        /// This is used to signal that the last known health status may no longer be applicable.
        /// </remarks>
        public void PauseReportingManagementAction(string managementGroupName, string managementActionName)
        {
            if (_cachedManagementGroupsRuntimeHealth.TryGetValue(managementGroupName, out Dictionary<string, RuntimeHealth?>? actionHealths))
            {
                actionHealths[managementActionName] = null;
            }
        }

        // should be called when the asset is deleted
        public async Task CancelHealthStatusReportingAsync(CancellationToken cancellationToken = default)
        {
            await _periodicSender.StopAsync(cancellationToken);
        }

        public async Task ReportDatasetHealthStatusAsync(string datasetName, RuntimeHealth datasetRuntimeHealth, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            var datasetHealthEvent = new DatasetsRuntimeHealthEvent()
            {
                DatasetName = datasetName,
                RuntimeHealth = datasetRuntimeHealth,
            };

            _cachedDatasetsRuntimeHealth.TryGetValue(datasetName, out RuntimeHealth? cachedHealth);
            RuntimeHealth.CompareNewHealthWithCachedHealth(datasetRuntimeHealth, cachedHealth, out bool updateCache, out bool sendIt);

            if (updateCache)
            {
                _cachedDatasetsRuntimeHealth[datasetName] = datasetRuntimeHealth;
            }

            if (sendIt)
            { 
                await _azureDeviceRegistryClient.ReportDatasetRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, new List<DatasetsRuntimeHealthEvent> { datasetHealthEvent }, telemetryTimeout, cancellationToken);
            }

            if ((updateCache || sendIt) && !_periodicSender.IsRunning())
            {
                await _periodicSender.StartAsync(cancellationToken);
            }
        }

        public async Task ReportDatasetHealthStatusAsync(List<DatasetsRuntimeHealthEvent> datasetRuntimeHealths, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            List<DatasetsRuntimeHealthEvent> eventsToReport = new();

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

                await _azureDeviceRegistryClient.ReportDatasetRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, eventsToReport, telemetryTimeout, cancellationToken);

            }
        }

        public async Task ReportStreamHealthStatusAsync(string streamName, RuntimeHealth streamRuntimeHealth, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            var streamsHealthEvent = new StreamsRuntimeHealthEvent()
            {
                StreamName = streamName,
                RuntimeHealth = streamRuntimeHealth,
            };

            await ReportStreamHealthStatusAsync(new List<StreamsRuntimeHealthEvent> { streamsHealthEvent }, telemetryTimeout, cancellationToken);
        }

        public async Task ReportStreamHealthStatusAsync(List<StreamsRuntimeHealthEvent> streamRuntimeHealths, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            List<StreamsRuntimeHealthEvent> eventsToReport = new();

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

            if (eventsToReport.Count > 0)
            {
                await _azureDeviceRegistryClient.ReportStreamRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, eventsToReport, telemetryTimeout, cancellationToken);
            }
        }

        public async Task ReportEventHealthStatusAsync(string eventGroupName, string eventName, RuntimeHealth eventRuntimeHealth, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            var eventsHealthEvent = new EventsRuntimeHealthEvent()
            {
                EventGroupName = eventGroupName,
                EventName = eventName,
                RuntimeHealth = eventRuntimeHealth,
            };

            RuntimeHealth? cachedHealth = null;
            if (_cachedEventGroupsRuntimeHealth.TryGetValue(eventGroupName, out Dictionary<string, RuntimeHealth?>? events))
            {
                events.TryGetValue(eventName, out cachedHealth);
            }

            RuntimeHealth.CompareNewHealthWithCachedHealth(eventRuntimeHealth, cachedHealth, out bool updateCache, out bool sendIt);

            if (updateCache)
            {
                if (!_cachedEventGroupsRuntimeHealth.ContainsKey(eventGroupName))
                {
                    _cachedEventGroupsRuntimeHealth[eventGroupName] = new();
                }

                _cachedEventGroupsRuntimeHealth[eventGroupName][eventName] = eventRuntimeHealth;
            }

            if (sendIt)
            {
                await _azureDeviceRegistryClient.ReportEventRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, new List<EventsRuntimeHealthEvent> { eventsHealthEvent }, telemetryTimeout, cancellationToken);
            }

            if ((updateCache || sendIt) && !_periodicSender.IsRunning())
            {
                await _periodicSender.StartAsync(cancellationToken);
            }
        }

        public async Task ReportEventHealthStatusAsync(List<EventsRuntimeHealthEvent> eventRuntimeHealths, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            List<EventsRuntimeHealthEvent> eventsToReport = new();
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

            if (eventsToReport.Count > 0)
            {
                await _azureDeviceRegistryClient.ReportEventRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, eventsToReport, telemetryTimeout, cancellationToken);
            }
        }

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

        public async Task ReportManagementActionHealthStatusAsync(List<ManagementActionsRuntimeHealthEvent> managementActionRuntimeHealths, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            List<ManagementActionsRuntimeHealthEvent> eventsToReport = new();
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

            if (eventsToReport.Count > 0)
            {
                await _azureDeviceRegistryClient.ReportManagementActionRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, eventsToReport, telemetryTimeout, cancellationToken);
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
            try
            {
                await _periodicSender.StopAsync();
                _periodicSender?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // It's fine if this is already disposed.
            }
        }

        private async Task SendPeriodicReportAsync(CancellationToken cancellationToken)
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
    }
}
