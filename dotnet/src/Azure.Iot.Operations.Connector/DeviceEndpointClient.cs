// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A client for reporting the status of this device and its endpoint
    /// </summary>
    public class DeviceEndpointClient
    {
        private readonly IAdrClientWrapper _adrClient;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;

        // Used to make getAndUpdate calls behave atomically. Also respected by get and update methods so that a user
        // does not accidentally update a device while another thread is in the middle of a getAndUpdate call.
        private readonly SemaphoreSlim _semaphore = new(0, 1);

        internal DeviceEndpointClient(IAdrClientWrapper adrClient, string deviceName, string inboundEndpointName)
        {
            _adrClient = adrClient;
            _deviceName = deviceName;
            _inboundEndpointName = inboundEndpointName;
        }

        /// <summary>
        /// Get the current status of this device and then optionally update it.
        /// </summary>
        /// <param name="handler">The function that determines the new device status when given the current device status.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="commandTimeout">The timeout for each of the 'get' and 'update' commands.</param>
        /// <returns>The latest device status after this operation.</returns>
        /// <remarks>
        /// If after retrieving the current status, you don't want to send any updates, <paramref name="handler"/> should return null.
        /// If this happens, this function will return the latest asset status without trying to update it.
        /// </remarks>
        public async Task<DeviceStatus> GetAndUpdateDeviceStatusAsync(Func<DeviceStatus, DeviceStatus?> handler, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                DeviceStatus currentStatus = await GetDeviceStatusAsync(commandTimeout, cancellationToken);
                DeviceStatus? desiredStatus = handler.Invoke(currentStatus);

                if (desiredStatus != null)
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
        /// <param name="deviceName">The name of the device.</param>
        /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
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
    }
}
