// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry
{
    public class HealthStatusReporter : IDisposable
    {
        public static HealthStatusReporter CreateDeviceEndpointHealthStatusReporter(
            IAzureDeviceRegistryClient azureDeviceRegistryClient,
            string deviceName,
            string inboundEndpointName)
        {
            Func<RuntimeHealth, TimeSpan?, CancellationToken, Task> func = async (health, timespan, cancellationToken) =>
            {
                await azureDeviceRegistryClient.ReportDeviceEndpointRuntimeHealthAsync(deviceName, inboundEndpointName, health, timespan, cancellationToken);
            };

            return new HealthStatusReporter(func);
        }

        public static HealthStatusReporter CreateDatasetHealthStatusReporter(
            IAzureDeviceRegistryClient azureDeviceRegistryClient,
            string deviceName,
            string inboundEndpointName,
            string assetName,
            string datasetName)
        {
            Func<RuntimeHealth, TimeSpan?, CancellationToken, Task> func = async (health, timespan, cancellationToken) =>
            {
                var datasetRuntimeHealthList = new List<DatasetsRuntimeHealthEvent>()
                {
                    new DatasetsRuntimeHealthEvent()
                    {
                        DatasetName = datasetName,
                        RuntimeHealth = health,
                    }
                };

                await azureDeviceRegistryClient.ReportDatasetRuntimeHealthAsync(deviceName, inboundEndpointName, assetName, datasetRuntimeHealthList, timespan, cancellationToken);
            };

            return new HealthStatusReporter(func);
        }

        public static HealthStatusReporter CreateEventHealthStatusReporter(
            IAzureDeviceRegistryClient azureDeviceRegistryClient,
            string deviceName,
            string inboundEndpointName,
            string assetName,
            string eventName)
        {
            Func<RuntimeHealth, TimeSpan?, CancellationToken, Task> func = async (health, timespan, cancellationToken) =>
            {
                var eventRuntimeHealthList = new List<EventsRuntimeHealthEvent>()
                {
                    new EventsRuntimeHealthEvent()
                    {
                        EventName = eventName,
                        RuntimeHealth = health,
                    }
                };

                await azureDeviceRegistryClient.ReportEventRuntimeHealthAsync(deviceName, inboundEndpointName, assetName, eventRuntimeHealthList, timespan, cancellationToken);
            };

            return new HealthStatusReporter(func);
        }

        private RuntimeHealth? _lastSentHealthStatus = null;
        private Countdown? _periodicSender; //TODO would it be feasible to have a single static sender? Don't want 1 thread per dataset/stream/endpoint/etc since that scale explodes
        // rust uses some thread pool concept here. Probably can in .NET too?

        // This is the function that this reporter will call to report a particular health status. This is defined generically
        // so that we can re-use this class to handle scheduling health status updates for device endpoints, streams, assets, datasets, etc.
        private readonly Func<RuntimeHealth, TimeSpan?, CancellationToken, Task> _adrClientMethod;

        internal HealthStatusReporter(Func<RuntimeHealth, TimeSpan?, CancellationToken, Task> adrClientMethod)
        {
            _adrClientMethod = adrClientMethod;
        }

        /// <summary>
        /// Pause background reporting until a new health status is set
        /// </summary>
        /// <remarks>
        /// This is used to signal that the last known health status may no longer be applicable.
        /// </remarks>
        public async Task PauseReportingAsync(CancellationToken cancellationToken)
        {
            _lastSentHealthStatus = null;
            if (_periodicSender != null)
            {
                await _periodicSender.StopAsync(cancellationToken);
            }
        }

        // should be called when the device endpoint is deleted
        public async Task CancelHealthStatusReportingAsync(CancellationToken cancellationToken = default)
        {
            if (_periodicSender != null)
            {
                await _periodicSender.StopAsync(cancellationToken); // TODO is this actually any different from pausing?
            }
        }

        public async Task ReportHealthStatusAsync(RuntimeHealth deviceEndpointHealth, TimeSpan? backgroundReportInterval = default, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            if (backgroundReportInterval == default)
            {
                backgroundReportInterval = TimeSpan.FromSeconds(10); //TODO const
            }

            if (_lastSentHealthStatus == null)
            {
                // This is the first health status event (or the first status event since the user paused reporting), so report it and start periodically reporting
                await SendHealthStatusAndResetCountdownAsync(deviceEndpointHealth, backgroundReportInterval!.Value, telemetryTimeout, cancellationToken);
                return;
            }

            if (RuntimeHealth.Equals(deviceEndpointHealth, _lastSentHealthStatus))
            {
                // The reported health status is no different than the last reported status, so do nothing. This last reported status
                // will be sent by the background reporting later if it doesn't change prior to the next period.
                return;
            }

            if (deviceEndpointHealth.Version < _lastSentHealthStatus.Version)
            {
                // The reported health status belongs to an older version, so it should not be reported or cached
                return;
            }

            if (RuntimeHealth.EqualsExceptTimestamp(deviceEndpointHealth, _lastSentHealthStatus) && deviceEndpointHealth.LastUpdateTime.CompareTo(_lastSentHealthStatus.LastUpdateTime) >= 0)
            {
                // The new health status is identical to the previously sent status, but with a newer timestamp. Just update the timestamp
                // of the cached version so that it is sent on the next background report.
                _lastSentHealthStatus.LastUpdateTime = deviceEndpointHealth.LastUpdateTime;
                return;
            }

            // The reported health status is different enough from the last sent status that it should actually be sent to the service and the periodic reporting timer should be restarted
            await SendHealthStatusAndResetCountdownAsync(deviceEndpointHealth, backgroundReportInterval!.Value, telemetryTimeout, cancellationToken);
        }

        // Send the cached health status unless reporting has been paused
        private async Task SendReportedHealthStatusIfNeededAsync(CancellationToken cancellationToken)
        {
            if (_lastSentHealthStatus != null)
            {
                await _adrClientMethod.Invoke(_lastSentHealthStatus, null, cancellationToken);
            }
        }

        private async Task SendHealthStatusAndResetCountdownAsync(RuntimeHealth health, TimeSpan backgroundReportInterval, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            if (_periodicSender != null)
            {
                await _periodicSender.StopAsync();
                _periodicSender.Dispose();
                _periodicSender = new Countdown(backgroundReportInterval, SendReportedHealthStatusIfNeededAsync);
            }

            await _adrClientMethod.Invoke(health, telemetryTimeout, cancellationToken);
            _lastSentHealthStatus = health;

            // Calling start while already started just resets the countdown
            await _periodicSender!.StartAsync(cancellationToken);
        }

        public void Dispose()
        {
            _periodicSender?.Dispose();
        }
    }
}
