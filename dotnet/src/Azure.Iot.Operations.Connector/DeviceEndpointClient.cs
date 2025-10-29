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
        public string DeviceName { get; internal set; }

        public string InboundEndpointName { get; internal set; }

        public Device Device { get; internal set; }

        public DeviceEndpointStatus DeviceEndpointStatus { get; internal set; }

        public DeviceEndpointClient(string deviceName, string inboundEndpointName, Device device, DeviceEndpointStatus deviceEndpointStatus)
        {
            DeviceName = deviceName;
            InboundEndpointName = inboundEndpointName;
            Device = device;
            DeviceEndpointStatus = deviceEndpointStatus;
        }
    }
}
