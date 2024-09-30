// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public class AzureDeviceRegistryClient : IAsyncDisposable
    {
#pragma warning disable CS0067 // Unused for now
        public event EventHandler<Asset>? AssetChanged;
        public event EventHandler<AssetEndpointProfile>? AssetEndpointProfileChanged;
#pragma warning restore CS0067 // Unused for now

        public AzureDeviceRegistryClient()
        {
        }

        /// <summary>
        /// Get the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset to retrieve.</param>
        /// <returns>The requested asset.</returns>
        public Task<Asset> GetAssetAsync(string assetId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the asset endpoint profile of the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset whose endpoint profile to retrieve.</param>
        /// <returns>The requested asset endpoint profile.</returns>
        public Task<AssetEndpointProfile> GetAssetEndpointProfileAsync(string assetId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Start receiving notifications on <see cref="AssetFileChanged"/> when the asset with the provided Id changes.
        /// </summary>
        /// <param name="assetId">The Id of the asset to observe.</param>
        public Task ObserveAssetAsync(string assetId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetFileChanged"/> when the asset with the provided Id changes.
        /// </summary>
        /// <param name="assetId">The Id of the asset to unobserve.</param>
        public Task UnobserveAssetAsync(string assetId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Start receiving notifications on <see cref="AssetEndpointProfileFileChanged"/> when the asset endpoint profile
        /// changes for the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset whose endpoint profile you want to observe.</param>
        public Task ObserveAssetEndpointProfileAsync(string assetId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetEndpointProfileFileChanged"/> when the asset endpoint profile
        /// changes for the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset whose endpoint profile you want to unobserve.</param>
        public Task UnobserveAssetEndpointProfileAsync(string assetId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the complete list of assets deployed by the operator to this pod.
        /// </summary>
        /// <returns>The complete list of assets deployed by the operator to this pod.</returns>
        public Task<IEnumerable<string>> GetAssetIdsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Dispose this client and all its resources.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }
}
