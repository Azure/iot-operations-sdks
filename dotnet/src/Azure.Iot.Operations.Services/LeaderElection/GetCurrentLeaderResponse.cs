// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.LeaderElection
{
    public class GetCurrentLeaderResponse
    {
        /// <summary>
        /// The current leader.
        /// </summary>
        /// <remarks>
        /// This value is null if there is no current leader.
        /// </remarks>
        public LeaderElectionCandidate? CurrentLeader { get; set; }

        internal GetCurrentLeaderResponse(LeaderElectionCandidate? currentLeader)
        {
            CurrentLeader = currentLeader;
        }
    }
}
