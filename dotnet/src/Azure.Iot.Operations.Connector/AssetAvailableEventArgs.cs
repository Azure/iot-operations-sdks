// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Azure.Iot.Operations.Services.LeaderElection;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// The event args for when an asset becomes available to sample.
    /// </summary>
    public class AssetAvailableEventArgs : EventArgs, IAsyncDisposable
    {
        /// <summary>
        /// The name of the device that this asset belongs to.
        /// </summary>
        public string DeviceName { get; }

        /// <summary>
        /// The device that this asset belongs to.
        /// </summary>
        public Device Device { get; }

        /// <summary>
        /// The name of the endpoint that this asset belongs to.
        /// </summary>
        public string InboundEndpointName { get; }

        /// <summary>
        /// The name of the asset that is now available to sample.
        /// </summary>
        public string AssetName { get; }

        /// <summary>
        /// The asset that is now available to sample.
        /// </summary>
        public Asset Asset { get; }

        /// <summary>
        /// The leader election client used by this connector. It is null if and only if this connector isn't configured to do leader election.
        /// </summary>
        /// <remarks>
        /// When configured to use leader election, <see cref="ConnectorWorker"/> automatically subscribes to notifications about leadership position
        /// changes using this client and will automatically trigger the cancellation token provided in <see cref="ConnectorWorker.WhileAssetIsAvailable"/>
        /// if it detects that this connector is no longer the leader.
        ///
        /// This client can still be used within <see cref="ConnectorWorker.WhileAssetIsAvailable"/> to check the leadership position manually, though.
        ///
        /// Users should not attempt to close or dispose this client as the connector will do that for you when appropriate.
        /// </remarks>
        public ILeaderElectionClient? LeaderElectionClient { get; }

        /// <summary>
        /// The client to use to send status updates for assets on and to use to forward sampled datasets/received events with.
        /// </summary>
        public AssetClient AssetClient { get; }

        /// <summary>
        /// The client to use to send status updates for this asset's device on.
        /// </summary>
        public DeviceEndpointClient DeviceEndpointClient { get; }

        /// <summary>
        /// The ADR client used by the worker — exposed to internal SDK collaborators
        /// (e.g. <see cref="ManagementActionOrchestrator"/>) that need endpoint credentials
        /// or other ADR data not surfaced through <see cref="AssetClient"/>.
        /// </summary>
        internal IAzureDeviceRegistryClientWrapper AdrClient { get; }

        // When true, DisposeAsync skips disposing AssetClient — used when this args instance
        // borrows an AssetClient that outlives a single asset revision (rebuilt on Updated by
        // ConnectorWorker so the user-supplied WhileAssetIsAvailable callback gets a fresh
        // CancellationToken without tearing down management-action state).
        private readonly bool _ownsAssetClient;

        internal AssetAvailableEventArgs(string deviceName, Device device, string inboundEndpointName, string assetName, Asset asset, ILeaderElectionClient? leaderElectionClient, IAzureDeviceRegistryClientWrapper adrClient, ConnectorWorker connector)
        {
            DeviceName = deviceName;
            Device = device;
            InboundEndpointName = inboundEndpointName;
            AssetName = assetName;
            Asset = asset;
            LeaderElectionClient = leaderElectionClient;
            AssetClient = new(adrClient, deviceName, inboundEndpointName, assetName, connector, device, asset);
            DeviceEndpointClient = new(adrClient, deviceName, inboundEndpointName, device);
            AdrClient = adrClient;
            _ownsAssetClient = true;
        }

        /// <summary>
        /// Borrow-mode ctor: wraps an existing <see cref="AssetClient"/> (and a fresh
        /// <see cref="DeviceEndpointClient"/>) without taking ownership of it. Used by
        /// <see cref="ConnectorWorker"/> when an asset is updated: a new event-args
        /// instance is handed to the user-supplied
        /// <see cref="ConnectorWorker.WhileAssetIsAvailable"/> callback so the user gets a
        /// fresh <see cref="CancellationToken"/>, but the management-action state on the
        /// underlying <see cref="AssetClient"/> is preserved across the update.
        /// </summary>
        internal AssetAvailableEventArgs(string deviceName, Device device, string inboundEndpointName, string assetName, Asset asset, ILeaderElectionClient? leaderElectionClient, IAzureDeviceRegistryClientWrapper adrClient, AssetClient borrowedAssetClient)
        {
            DeviceName = deviceName;
            Device = device;
            InboundEndpointName = inboundEndpointName;
            AssetName = assetName;
            Asset = asset;
            LeaderElectionClient = leaderElectionClient;
            AssetClient = borrowedAssetClient;
            DeviceEndpointClient = new(adrClient, deviceName, inboundEndpointName, device);
            AdrClient = adrClient;
            _ownsAssetClient = false;
        }

        public virtual async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            GC.SuppressFinalize(this);
        }

        public virtual async ValueTask DisposeAsync(bool disposing)
        {
            await DisposeAsyncCore();
        }

        private async ValueTask DisposeAsyncCore()
        {
            if (_ownsAssetClient)
            {
                try
                {
                    await AssetClient.DisposeAsync();
                }
                catch (ObjectDisposedException)
                {
                    // It's fine if this is already disposed
                }
            }

            try
            {
                await DeviceEndpointClient.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
                // It's fine if this is already disposed
            }
        }
    }
}
