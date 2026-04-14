// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A client for reporting the status of this device and its endpoint
    /// </summary>
    public class DeviceEndpointClient : IAsyncDisposable
    {
        private readonly IAzureDeviceRegistryClientWrapper _adrClient;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;
        private readonly Device _device;
        private readonly DeviceEndpointRuntimeHealthReporter _healthReporter;

        // Used to make getAndUpdate calls behave atomically so that a user does not accidentally
        // update a device while another thread is in the middle of a getAndUpdate call.
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        internal DeviceEndpointClient(IAzureDeviceRegistryClientWrapper adrClient, string deviceName, string inboundEndpointName, Device device)
        {
            _adrClient = adrClient;
            _deviceName = deviceName;
            _inboundEndpointName = inboundEndpointName;
            _device = device;
            _healthReporter = new(adrClient.GetWrapped(), deviceName, inboundEndpointName, TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Get the current status of this device and then optionally update it.
        /// </summary>
        /// <param name="handler">The function that determines the new device status when given the current device status.</param>
        /// <param name="onlyIfChanged">
        /// Only send the status update if the new status is different from the current status. If the only
        /// difference between the current and new status is a 'LastTransitionTime' field, then the statuses will be
        /// considered identical.
        /// </param>
        /// <param name="commandTimeout">The timeout for each of the 'get' and 'update' commands.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The latest device status after this operation.</returns>
        /// <remarks>
        /// If after retrieving the current status, you don't want to send any updates, <paramref name="handler"/> should return null.
        /// If this happens, this function will return the latest asset status without trying to update it.
        ///
        /// This method uses a semaphore to ensure that this same client doesn't accidentally update the device status while
        /// another thread is in the middle of updating the same device. This ensures that the current device status provided in <paramref name="handler"/>
        /// stays accurate while any updating occurs.
        /// </remarks>
        public async Task<DeviceStatus> GetAndUpdateDeviceStatusAsync(Func<DeviceStatus, DeviceStatus?> handler, bool onlyIfChanged = false, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                DeviceStatus currentStatus = await GetDeviceStatusAsync(commandTimeout, cancellationToken);
                DeviceStatus? desiredStatus = handler.Invoke(currentStatus);

                if (desiredStatus != null && (!onlyIfChanged || currentStatus.EqualTo(desiredStatus)))
                {
                    return await UpdateDeviceStatusAsync(desiredStatus, commandTimeout, cancellationToken);
                }

                return currentStatus;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Update the status of a specific device in the Azure Device Registry service
        /// </summary>
        /// <param name="status">The new status of the device.</param>
        /// <param name="commandTimeout">Optional timeout for the command.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The status returned by the Azure Device Registry service</returns>
        /// <remarks>
        /// This update call will act as a 'patch' for all endpoint-level statuses, but will act as a 'put' for the device-level status.
        /// That means that, for devices with multiple endpoints, you can safely call this method when each endpoint has a status to
        /// report without needing to include the existing status of previously reported endpoints.
        /// </remarks>
        private async Task<DeviceStatus> UpdateDeviceStatusAsync(
            DeviceStatus status,
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return await _adrClient.UpdateDeviceStatusAsync(
                _deviceName,
                _inboundEndpointName,
                status,
                commandTimeout,
                cancellationToken);
        }

        /// <summary>
        /// Get the status of this device from the Azure Device Registry service
        /// </summary>
        /// <param name="commandTimeout">The timeout for this RPC command invocation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The status returned by the Azure Device Registry service</returns>
        private async Task<DeviceStatus> GetDeviceStatusAsync(
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return await _adrClient.GetDeviceStatusAsync(
                _deviceName,
                _inboundEndpointName,
                commandTimeout,
                cancellationToken);
        }

        /// <summary>
        /// Report this device endpoint's runtime health.
        /// </summary>
        /// <param name="runtimeHealth">The runtime health of this device endpoint.</param>
        /// <param name="telemetryTimeout">The timeout to use when sending this telemetry if any telemetry is sent.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This method uses the <see cref="DeviceEndpointRuntimeHealthReporter"/> class, so it will de-duplicate runtime healths and will periodically report the last known
        /// runtime health for as long as this device endpoint is available. Because of that, connector applications can freely call this method repeatedly even if the runtime health hasn't changed.
        /// </remarks>
        public async Task ReportRuntimeHealthAsync(ConnectorRuntimeHealth runtimeHealth, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            RuntimeHealth servicesRuntimeHealth = new()
            {
                Message = runtimeHealth.Message,
                ReasonCode = runtimeHealth.ReasonCode,
                Status = runtimeHealth.Status,
                Version = _device.Version ?? 0,
                LastUpdateTime = DateTime.UtcNow,
            };

            await _healthReporter.ReportDeviceEndpointRuntimeHealthAsync(servicesRuntimeHealth, telemetryTimeout, cancellationToken);
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
                _semaphore.Dispose();
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
