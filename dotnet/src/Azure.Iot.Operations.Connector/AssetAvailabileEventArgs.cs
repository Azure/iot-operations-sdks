// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector
{
    public class AssetAvailabileEventArgs : EventArgs
    {
        public Asset Asset { get; }

        public AssetAvailabileEventArgs(Asset asset)
        {
            Asset = asset;
        }
    }
}
