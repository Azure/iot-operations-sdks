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

        /// <summary>
        /// Wraps an <see cref="AssetClient"/> (whose lifetime is owned by <see cref="ConnectorWorker"/>'s
        /// per-asset runtime context) together with a fresh <see cref="DeviceEndpointClient"/>. This
        /// instance never disposes the <see cref="AssetClient"/>: the same client is shared across the
        /// management-action branch and successive user-callback branches (rebuilt on Updated so the user
        /// gets a fresh <see cref="CancellationToken"/>) without tearing down management-action state.
        /// </summary>
        internal AssetAvailableEventArgs(string deviceName, Device device, string inboundEndpointName, string assetName, Asset asset, ILeaderElectionClient? leaderElectionClient, IAzureDeviceRegistryClientWrapper adrClient, AssetClient assetClient)
        {
            DeviceName = deviceName;
            Device = device;
            InboundEndpointName = inboundEndpointName;
            AssetName = assetName;
            Asset = asset;
            LeaderElectionClient = leaderElectionClient;
            AssetClient = assetClient;
            DeviceEndpointClient = new(adrClient, deviceName, inboundEndpointName, device);
            AdrClient = adrClient;
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
            // The AssetClient is owned by the per-asset runtime context, not by this args instance —
            // it outlives individual args and is disposed by the context.
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
