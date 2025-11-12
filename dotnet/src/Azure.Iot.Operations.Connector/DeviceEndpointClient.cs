// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

        internal DeviceEndpointClient(IAdrClientWrapper adrClient, string deviceName, string inboundEndpointName)
        {
            _adrClient = adrClient;
            _deviceName = deviceName;
            _inboundEndpointName = inboundEndpointName;
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
        public Task<DeviceStatus> UpdateDeviceStatusAsync(
            DeviceStatus status,
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return _adrClient.UpdateDeviceStatusAsync(
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
        public Task<DeviceStatus> GetDeviceStatusAsync(
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return _adrClient.GetDeviceStatusAsync(
                _deviceName,
                _inboundEndpointName,
                commandTimeout,
                cancellationToken);
        }
    }
}
