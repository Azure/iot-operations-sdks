﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// The event args for when an asset becomes available to sample.
    /// </summary>
    public class AssetAvailabileEventArgs : EventArgs
    {
        public Device Device { get; }

        public string InboundEndpointName { get; }

        /// <summary>
        /// The name of the asset that is now available to sample.
        /// </summary>
        public string AssetName { get; }

        /// <summary>
        /// The asset that is now available to sample.
        /// </summary>
        public Asset Asset { get; }

        internal AssetAvailabileEventArgs(Device device, string inboundEndpointName, string assetName, Asset asset)
        {
            Device = device;
            InboundEndpointName = inboundEndpointName;
            AssetName = assetName;
            Asset = asset;
        }
    }
}
