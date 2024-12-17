// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.LeaderElection
{
    public sealed class LeadershipChangeEventArgs : EventArgs
    {
        /// <summary>
        /// The new state of the leadership position.
        /// </summary>
        public LeadershipPositionState NewState { get; internal set; }

        /// <summary>
        /// The new leader. This value is null if this update is that the previous leader resigned or ended their term.
        /// </summary>
        public LeaderElectionCandidate? NewLeader { get; internal set; }

        /// <summary>
        /// The timestamp associated with this event.
        /// </summary>
        public HybridLogicalClock Timestamp { get; internal set; }

        internal LeadershipChangeEventArgs(LeaderElectionCandidate? newLeader, HybridLogicalClock timestamp)
        {
            NewLeader = newLeader;
            Timestamp = timestamp;
        }
    }
}
