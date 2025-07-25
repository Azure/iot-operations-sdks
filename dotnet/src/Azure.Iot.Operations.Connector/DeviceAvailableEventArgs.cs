// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Azure.Iot.Operations.Services.LeaderElection;

namespace Azure.Iot.Operations.Connector
{
    public class DeviceAvailableEventArgs : EventArgs
    {
        public Device Device { get; }

        public string InboundEndpointName { get; }

        /// <summary>
        /// The leader election client used by this connector. It is null if and only if this connector isn't configured to do leader election.
        /// </summary>
        /// <remarks>
        /// When configured to use leader election, <see cref="ConnectorWorker"/> automatically subscribes to notifications about leadership position
        /// changes using this client and will automatically trigger the cancellation token provided in <see cref="ConnectorWorker.WhileDeviceIsAvailable"/>
        /// if it detects that this connector is no longer the leader.
        ///
        /// This client can still be used within <see cref="ConnectorWorker.WhileDeviceIsAvailable"/> to check the leadership position manually, though.
        ///
        /// Users should not attempt to close or dispose this client as the connector will do that for you when appropriate.
        /// </remarks>
        public ILeaderElectionClient? LeaderElectionClient { get; }

        internal DeviceAvailableEventArgs(Device device, string inboundEndpointName, ILeaderElectionClient? leaderElectionClient)
        {
            Device = device;
            InboundEndpointName = inboundEndpointName;
            LeaderElectionClient = leaderElectionClient;
        }
    }
}
