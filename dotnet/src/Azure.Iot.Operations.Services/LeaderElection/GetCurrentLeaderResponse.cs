// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
