// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
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

        //TODO must include endpoint status always, correct?
        public async Task<DeviceStatus> UpdateDeviceStatusAsync(
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
    }
}
