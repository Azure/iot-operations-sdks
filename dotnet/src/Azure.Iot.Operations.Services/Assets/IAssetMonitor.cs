// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Assets
{
    public interface IAssetMonitor
    {
        /// <summary>
        /// The callback that executes when an asset has changed once you start observing an asset with
        /// <see cref="ObserveAssets(TimeSpan?, CancellationToken)"/>.
        /// </summary>
        event EventHandler<AssetChangedEventArgs>? AssetChanged;

        /// <summary>
        /// The callback that executes when the asset endpoint profile has changed once you start observing it with
        /// <see cref="ObserveAssetEndpointProfile(TimeSpan?, CancellationToken)"/>.
        /// </summary>
        event EventHandler<AssetEndpointProfileChangedEventArgs>? AssetEndpointProfileChanged;

        /// <summary>
        /// Get the asset with the provided Id.
        /// </summary>
        /// <param name="assetName">The Id of the asset to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The requested asset.</returns>
        Task<Asset?> GetAssetAsync(string assetName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the asset endpoint profile of the asset with the provided Id.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The requested asset endpoint profile. This value may be null if no asset endpoint profile is configured yet.</returns>
        Task<AssetEndpointProfile?> GetAssetEndpointProfileAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Start receiving notifications on <see cref="AssetChanged"/> when any asset changes.
        /// </summary>
        /// <param name="pollingInterval">How frequently to check for changes to the asset.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        void ObserveAssets(TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetChanged"/> when an asset changes.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        void UnobserveAssets(CancellationToken cancellationToken = default);

        /// <summary>
        /// Start receiving notifications on <see cref="AssetEndpointProfileChanged"/> when the asset endpoint profile
        /// changes for the asset with the provided Id.
        /// </summary>
        /// <param name="pollingInterval">How frequently to check for changes to the asset endpoint profile.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        void ObserveAssetEndpointProfile(TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetEndpointProfileChanged"/> when the asset endpoint profile
        /// changes for the asset with the provided Id.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        void UnobserveAssetEndpointProfile(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the complete list of assets deployed by the operator to this pod.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The complete list of assets deployed by the operator to this pod.</returns>
        Task<List<string>> GetAssetNamesAsync(CancellationToken cancellationToken = default);
    }
}
