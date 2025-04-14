﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public class MockAssetMonitor : IAssetFileMonitor
    {
        public event EventHandler<AssetCreatedEventArgs>? AssetCreated;
        public event EventHandler<AssetDeletedEventArgs>? AssetDeleted;
        public event EventHandler<AssetEndpointProfileCreatedEventArgs>? AssetEndpointProfileCreated;
        public event EventHandler<AssetEndpointProfileDeletedEventArgs>? AssetEndpointProfileDeleted;

        public List<string> AssetEndpointProfileNames { get; set; } = new();

        public List<string> AssetNames { get; set; } = new();

        private List<string> _observedAssetEndpointProfileAssetNames = new();

        private bool _isObservingAssetEndpointProfiles = false;

        public void SimulateNewAssetEndpointProfileCreated(AssetEndpointProfile aep)
        {
            if (_isObservingAssetEndpointProfiles)
            {
                AssetEndpointProfileCreated?.Invoke(this, new(aep.Name, aep));
            }
        }

        public void SimulateNewAssetEndpointProfileDeleted(string aepName)
        {
            if (_isObservingAssetEndpointProfiles)
            {
                AssetEndpointProfileDeleted?.Invoke(this, new(aepName));
            }
        }

        public void SimulateNewAssetCreated(string aepName, string assetName, Asset asset)
        {
            if (_observedAssetEndpointProfileAssetNames.Contains(aepName))
            {
                AssetCreated?.Invoke(this, new(aepName, assetName, asset));
            }
        }

        public void SimulateNewAssetDeleted(string aepName, string assetName)
        {
            if (_observedAssetEndpointProfileAssetNames.Contains(aepName))
            {
                AssetDeleted?.Invoke(this, new(aepName, assetName));
            }
        }

        public List<string> GetAssetEndpointProfileNames()
        {
            return AssetEndpointProfileNames;
        }

        public List<string> GetAssetNames()
        {
            return AssetNames;
        }

        public void ObserveAssets(string aepName)
        {
            _observedAssetEndpointProfileAssetNames.Add(aepName);
        }

        public void ObserveAssetEndpointProfiles()
        {
            _isObservingAssetEndpointProfiles = true;
        }

        public void UnobserveAssetEndpointProfiles()
        {
            _isObservingAssetEndpointProfiles = false;
        }

        public void UnobserveAssets(string aepName)
        {
            _observedAssetEndpointProfileAssetNames.Remove(aepName);
        }

        public void UnobserveAll()
        {
            _observedAssetEndpointProfileAssetNames.Clear();
            _isObservingAssetEndpointProfiles = false;
        }

        public void Dispose()
        {
            // Nothing needs to be disposed in this mock implementation
        }
    }
}
