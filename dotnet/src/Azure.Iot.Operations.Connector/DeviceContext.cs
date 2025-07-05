// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    internal class DeviceContext
    {
        public string DeviceName { get; set; }

        public string InboundEndpointName { get; set; }

        public Device Device { get; set; }

        public ConcurrentDictionary<string, Asset> Assets { get; set; } = new();

        public DeviceContext(string deviceName, string inboundEndpointName, Device device)
        {
            DeviceName = deviceName;
            InboundEndpointName = inboundEndpointName;
            Device = device;
        }
    }
}
