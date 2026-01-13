// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A client for reporting the status of this device and its endpoint
    /// </summary>
    public class DeviceEndpointClient : IDisposable
    {
        private readonly IAzureDeviceRegistryClientWrapper _adrClient;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;

        // Used to make getAndUpdate calls behave atomically so that a user does not accidentally
        // update a device while another thread is in the middle of a getAndUpdate call.
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        internal DeviceEndpointClient(IAzureDeviceRegistryClientWrapper adrClient, string deviceName, string inboundEndpointName)
        {
            _adrClient = adrClient;
            _deviceName = deviceName;
            _inboundEndpointName = inboundEndpointName;
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
        /// Report the health of this device endpoint.
        /// </summary>
        /// <param name="deviceName">The name of the device.</param>
        /// <param name="inboundEndpointName">The name of the endpoint.</param>
        /// <param name="telemetry">The health status to report.</param>
        /// <param name="qos">The MQTT quality of service to send this report with.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ReportDeviceEndpointRuntimeHealthEvent(RuntimeHealth health, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            await _adrClient.ReportDeviceEndpointRuntimeHealthEvent(
                _deviceName,
                _inboundEndpointName,
                new()
                {
                    DeviceEndpointRuntimeHealthEvent = new()
                    {
                        RuntimeHealth = health,
                    }
                },
                qos,
                telemetryTimeout,
                cancellationToken);
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

        public void Dispose()
        {
            try
            {
                _semaphore.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // It's fine if this semaphore is already disposed.
            }
        }
    }
}
