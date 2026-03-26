// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry
{
    public class DeviceEndpointHealthStatusReporter : IDisposable
    {
        private readonly IAzureDeviceRegistryClient _azureDeviceRegistryClient;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;
        private RuntimeHealth? _lastSentHealthStatus = null;
        private Countdown? _periodicSender;

        internal DeviceEndpointHealthStatusReporter(IAzureDeviceRegistryClient azureDeviceRegistryClient, string deviceName, string inboundEndpointName)
        {
            _azureDeviceRegistryClient = azureDeviceRegistryClient;
            _deviceName = deviceName;
            _inboundEndpointName = inboundEndpointName;
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
                await _azureDeviceRegistryClient.ReportDeviceEndpointRuntimeHealthAsync(_deviceName, _inboundEndpointName, _lastSentHealthStatus, cancellationToken: cancellationToken);
            }
        }

        private async Task SendHealthStatusAndResetCountdownAsync(RuntimeHealth deviceEndpointHealth, TimeSpan backgroundReportInterval, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            if (_periodicSender != null)
            {
                await _periodicSender.StopAsync();
                _periodicSender.Dispose();
                _periodicSender = new Countdown(backgroundReportInterval, SendReportedHealthStatusIfNeededAsync);
            }

            await _azureDeviceRegistryClient.ReportDeviceEndpointRuntimeHealthAsync(_deviceName, _inboundEndpointName, deviceEndpointHealth, telemetryTimeout, cancellationToken);
            _lastSentHealthStatus = deviceEndpointHealth;

            // Calling start while already started just resets the countdown
            await _periodicSender!.StartAsync(cancellationToken);
        }

        public void Dispose()
        {
            _periodicSender?.Dispose();
        }
    }
}
