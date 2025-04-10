// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Assets
{
    public interface IAssetFileMonitor
    {
        /// <summary>
        /// The callback that executes when an asset has been created once you start observing assets with
        /// <see cref="ObserveAssets()"/>.
        /// </summary>
        event EventHandler<AssetCreatedEventArgs>? AssetCreated;

        /// <summary>
        /// The callback that executes when an asset has been deleted once you start observing assets with
        /// <see cref="ObserveAssets()"/>.
        /// </summary>
        event EventHandler<AssetDeletedEventArgs>? AssetDeleted;

        /// <summary>
        /// The callback that executes when the asset endpoint profile has been created once you start observing AEPs with
        /// <see cref="ObserveAssetEndpointProfile()"/>.
        /// </summary>
        event EventHandler<AssetEndpointProfileCreatedEventArgs>? AssetEndpointProfileCreated;

        /// <summary>
        /// The callback that executes when the asset endpoint profile has been deleted once you start observing AEPs with
        /// <see cref="ObserveAssetEndpointProfile()"/>.
        /// </summary>
        event EventHandler<AssetEndpointProfileDeletedEventArgs>? AssetEndpointProfileDeleted;

        /// <summary>
        /// Start receiving notifications on <see cref="AssetChanged"/> when any asset changes.
        /// </summary>
        /// <param name="pollingInterval">How frequently to check for changes to the asset.</param>
        void ObserveAssets();

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetChanged"/> when an asset changes.
        /// </summary>
        void UnobserveAssets();

        /// <summary>
        /// Start receiving notifications on <see cref="AssetEndpointProfileChanged"/> when the asset endpoint profile
        /// changes for the asset with the provided Id.
        /// </summary>
        /// <param name="pollingInterval">How frequently to check for changes to the asset endpoint profile.</param>
        void ObserveAssetEndpointProfile();

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetEndpointProfileChanged"/> when the asset endpoint profile
        /// changes for the asset with the provided Id.
        /// </summary>
        void UnobserveAssetEndpointProfile();

        /// <summary>
        /// Returns the complete list of assets deployed by the operator to this pod.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The complete list of assets deployed by the operator to this pod.</returns>
        List<string> GetAssetNames();

        List<string> GetAssetEndpointProfileNames();
    }
}
