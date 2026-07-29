// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry
{
    /// <summary>
    /// A class for smartly sending runtime health events for a specific device endpoint to the Azure Device Registry service
    /// </summary>
    /// <remarks>
    /// This class has two main features that differentiate it from just directly calling the runtime health update APIs like <see cref="IAzureDeviceRegistryClient.ReportDeviceEndpointRuntimeHealthAsync(string, string, RuntimeHealth, TimeSpan?, CancellationToken)"/>
    ///  1) De-duplication of device endpoint runtime healths. This allows you to write your connector code such that it repeatedly calls an API like <see cref="ReportDeviceEndpointRuntimeHealthAsync(RuntimeHealth, TimeSpan?, CancellationToken)"/>
    ///  even if the runtime health has not changed as this client will cache the last sent runtime health and check if the new runtime health actually needs to be forwarded to the service.
    ///  2) Periodic reporting of the last known runtime healths of the device endpoint with updated timestamps. Connectors are advised to periodically send these updates to ensure that
    ///  the Azure Device Registry service has an up-to-date picture of the runtime health of each device endpoint and this class handles that for you. Additionally, the periodic updates can
    ///  be paused when the device endpoint's runtime health becomes unknown. This background reporting period can be changed with <see cref="SetRuntimeHealthBackgroundReportingIntervalAsync(TimeSpan, CancellationToken)"/>.
    /// </remarks>
    public class DeviceEndpointRuntimeHealthReporter : IAsyncDisposable
    {
        private RuntimeHealth? _cachedDeviceEndpointRuntimeHealth;

        private Countdown _periodicSender;

        private readonly IAzureDeviceRegistryClient _azureDeviceRegistryClient;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;

        /// <summary>
        /// Create a new runtime health reporter for a given device endpoint
        /// </summary>
        /// <param name="azureDeviceRegistryClient">The Azure Device Registry client used to send these runtime health events.</param>
        /// <param name="deviceName">The name of the device.</param>
        /// <param name="inboundEndpointName">The name of the inbound endpoint</param>
        /// <param name="reportingPeriod">The interval at which to send background reports.</param>
        /// <remarks>
        /// Background reporting does not start until the first call to <see cref="ReportDeviceEndpointRuntimeHealthAsync(RuntimeHealth, TimeSpan?, CancellationToken)"/>.
        /// </remarks>
        public DeviceEndpointRuntimeHealthReporter(IAzureDeviceRegistryClient azureDeviceRegistryClient, string deviceName, string inboundEndpointName, TimeSpan? reportingPeriod = null)
        {
            _azureDeviceRegistryClient = azureDeviceRegistryClient;
            _deviceName = deviceName;
            _inboundEndpointName = inboundEndpointName;
            _periodicSender = new Countdown(reportingPeriod ?? TimeSpan.FromSeconds(10), SendPeriodicReportAsync);
        }

        /// <summary>
        /// Stop the background reporting of this device endpoint
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// Generally, this should be done if the runtime health of the device endpoint becomes unknown.
        /// </remarks>
        public async Task CancelHealthStatusReportingAsync(CancellationToken cancellationToken = default)
        {
            await _periodicSender.StopAsync(cancellationToken);
        }

        /// <summary>
        /// Report this device endpoint's runtime health if it is worth reporting compared to the previously sent runtime health.
        /// </summary>
        /// <param name="deviceEndpointRuntimeHealth">The device endpoint's runtime health.</param>
        /// <param name="telemetryTimeout">The timeout for sending this telemetry. This value is ignored if the runtime health isn't ruled worth sending.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task ReportDeviceEndpointRuntimeHealthAsync(RuntimeHealth deviceEndpointRuntimeHealth, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            RuntimeHealth.CompareNewHealthWithCachedHealth(deviceEndpointRuntimeHealth, _cachedDeviceEndpointRuntimeHealth, out bool updateCache, out bool sendIt);

            if (updateCache)
            {
                _cachedDeviceEndpointRuntimeHealth = deviceEndpointRuntimeHealth;
            }

            if (sendIt)
            { 
                await _azureDeviceRegistryClient.ReportDeviceEndpointRuntimeHealthAsync(_deviceName, _inboundEndpointName, deviceEndpointRuntimeHealth, telemetryTimeout, cancellationToken);
            }

            if ((updateCache || sendIt) && !_periodicSender.IsRunning())
            {
                await _periodicSender.StartAsync(cancellationToken);
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
            try
            {
                if (_periodicSender != null)
                {
                    await _periodicSender.StopAsync();
                    _periodicSender.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
                // It's fine if this is already disposed.
            }
        }

        private async Task SendPeriodicReportAsync(CancellationToken cancellationToken)
        {
            if (_cachedDeviceEndpointRuntimeHealth != null)
            {
                await _azureDeviceRegistryClient.ReportDeviceEndpointRuntimeHealthAsync(_deviceName, _inboundEndpointName, _cachedDeviceEndpointRuntimeHealth, cancellationToken: cancellationToken);
            }
        }
    }
}
