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
    public class DeviceEndpointStatusReporter
    {
        private readonly ConnectorContext _connectorContext;
        private readonly DeviceEndpointStatus _deviceEndpointStatus;
        private readonly Device _device;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;

        public Task<bool> ReportDeviceStatusIfUnchanged()
        {
        }

        public Task<bool> ReportEndpointStatusIfUnchanged()
        {
        }

        private DeviceEndpointStatus getCurrentAssetStatus(DeviceEndpointStatus deviceEndpointStatus, ulong assetVersion)
        {
            if (deviceEndpointStatus.ConfigStatus != null
                && deviceEndpointStatus.ConfigStatus.Version != null
                && deviceEndpointStatus.ConfigStatus.Version == assetVersion)
            {
                return deviceEndpointStatus;
            }

            return new DeviceEndpointStatus();
        }

        internal DeviceEndpointStatusReporter(
            ConnectorContext connectorContext,
            DeviceEndpointStatus deviceEndpointStatus,
            Device device,
            string deviceName,
            string inboundEndpointName)
        {
            _connectorContext = connectorContext;
            _deviceEndpointStatus = deviceEndpointStatus;
            _device = device;
            _deviceName = deviceName;
            _inboundEndpointName = inboundEndpointName;
        }
    }
}
