// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public class MockAssetMonitor : IAssetMonitor
    {
        public event EventHandler<AssetChangedEventArgs>? AssetChanged;
        public event EventHandler<AssetEndpointProfileChangedEventArgs>? AssetEndpointProfileChanged;

        public Dictionary<string, Asset> _assets = new();
        public AssetEndpointProfile? _assetEndpointProfile;

        public bool _isObservingAssets = false;
        public bool _isObservingAssetEndpointProfile = false;

        public void AddOrUpdateMockAsset(string assetName, Asset asset)
        {
            if (_assets.ContainsKey(assetName))
            {
                _assets[assetName] = asset;
                if (_isObservingAssets)
                { 
                    AssetChanged?.Invoke(this, new AssetChangedEventArgs(assetName, ChangeType.Updated, asset));
                }
            }
            else
            { 
                _assets.Add(assetName, asset);
                if (_isObservingAssets)
                { 
                    AssetChanged?.Invoke(this, new AssetChangedEventArgs(assetName, ChangeType.Created, asset));
                }
            }
        }

        public void DeleteMockAsset(string assetName)
        {
            if (_assets.Remove(assetName) && _isObservingAssets)
            {
                AssetChanged?.Invoke(this, new AssetChangedEventArgs(assetName, ChangeType.Deleted, null));
            }
        }

        public void AddOrUpdateMockAssetEndpointProfile(AssetEndpointProfile assetEndpointProfile)
        {
            if (_assetEndpointProfile != null)
            {
                _assetEndpointProfile = assetEndpointProfile;
                if (_isObservingAssetEndpointProfile)
                { 
                    AssetEndpointProfileChanged?.Invoke(this, new AssetEndpointProfileChangedEventArgs(ChangeType.Updated, assetEndpointProfile));
                }
            }
            else
            {
                _assetEndpointProfile = assetEndpointProfile;
                if (_isObservingAssetEndpointProfile)
                { 
                    AssetEndpointProfileChanged?.Invoke(this, new AssetEndpointProfileChangedEventArgs(ChangeType.Created, assetEndpointProfile));
                }
            }
        }

        public void DeleteMockAssetEndpointProfile()
        {
            if (_assetEndpointProfile != null)
            {
                _assetEndpointProfile = null;
                if (_isObservingAssetEndpointProfile)
                { 
                    AssetEndpointProfileChanged?.Invoke(this, new AssetEndpointProfileChangedEventArgs(ChangeType.Deleted, null));
                }
            }
        }

        public Task<Asset?> GetAssetAsync(string assetName, CancellationToken cancellationToken = default)
        {
            if (_assets.TryGetValue(assetName, out var asset))
            {
                return Task.FromResult(asset);
            }

            return null;
        }

        public Task<AssetEndpointProfile?> GetAssetEndpointProfileAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_assetEndpointProfile);
        }

        public Task<List<string>> GetAssetNamesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_assets.Keys.ToList<string>());
        }

        public void ObserveAssetEndpointProfile(TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        {
            _isObservingAssetEndpointProfile = true;
        }

        public void ObserveAssets(TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        {
            _isObservingAssets = true;
        }

        public void UnobserveAssetEndpointProfile(CancellationToken cancellationToken = default)
        {
            _isObservingAssetEndpointProfile = false;
        }

        public void UnobserveAssets(CancellationToken cancellationToken = default)
        {
            _isObservingAssets = false;
        }
    }
}
