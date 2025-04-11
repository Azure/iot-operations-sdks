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
    internal class AssetEndpointProfileContext
    {
        internal AssetEndpointProfile AssetEndpointProfile { get; set; }

        internal Dictionary<string, Asset> Assets { get; set; }
    }
}
