// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry
{
    // allows user to send individual health events (if they are diff from cached) and also sends a batch of those cached health events periodically regardless.
    // users can still stop the periodic sending of particular events/streams/datasets/etc without preventing the other events/streams/datasets/etc from their
    // periodic batch send. The periodic sending starts as soon as the first entity reports a health status and stops if all cached entries have been paused or
    // if the reporter is disposed.
    public class DeviceEndpointHealthStatusReporter : IAsyncDisposable
    {
        private RuntimeHealth? _cachedDeviceEndpointRuntimeHealth = new();

        private readonly Countdown _periodicSender;

        private readonly IAzureDeviceRegistryClient _azureDeviceRegistryClient;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;

        public DeviceEndpointHealthStatusReporter(IAzureDeviceRegistryClient azureDeviceRegistryClient, string deviceName, string inboundEndpointName, TimeSpan? reportingPeriod = null)
        {
            _azureDeviceRegistryClient = azureDeviceRegistryClient;
            _deviceName = deviceName;
            _inboundEndpointName = inboundEndpointName;
            _periodicSender = new Countdown(reportingPeriod ?? TimeSpan.FromSeconds(10), SendPeriodicReportAsync);
        }

        // should be called when the deviceEndpoint is deleted
        public async Task CancelHealthStatusReportingAsync(CancellationToken cancellationToken = default)
        {
            await _periodicSender.StopAsync(cancellationToken);
        }

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
                    _periodicSender?.Dispose();
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
