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
    public class AssetStatusReporter
    {
        private readonly ConnectorContext _connectorContext;
        private readonly AssetStatus _assetStatus;
        private readonly Asset _asset;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;
        private readonly string _assetName;

        public Task<bool> ReportAssetStatusIfUnchanged()
        {
            // Check if asset version has changed
            //_asset.Version
        }

        private AssetStatus getCurrentAssetStatus(AssetStatus assetStatus, ulong assetVersion)
        {
            if (assetStatus.Config != null
                && assetStatus.Config.Version != null
                && assetStatus.Config.Version == assetVersion)
            {
                return assetStatus;
            }

            return new AssetStatus();
        }

        internal AssetStatusReporter(
            ConnectorContext connectorContext,
            AssetStatus assetStatus,
            Asset asset,
            string deviceName,
            string inboundEndpointName,
            string assetName)
        {
            _connectorContext = connectorContext;
            _assetStatus = assetStatus;
            _asset = asset;
            _deviceName = deviceName;
            _inboundEndpointName = inboundEndpointName;
            _assetName = assetName;
        }
    }
}
