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
    public class AssetClient
    {
        public string DeviceName { get; internal set; }

        public string InboundEnpointName { get; internal set; }

        public string AssetName { get; internal set; }

        public Asset Asset { get; internal set; }

        public AssetStatus AssetStatus { get; internal set; }

        public Device Device { get; internal set; }

        public DeviceEndpointStatus DeviceEndpointStatus { get; internal set; }

        internal CancellationToken OnAssetDeletedCancellationToken { get; set; }
    }
}
