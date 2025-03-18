// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.LeaderElection
{
    /// <summary>
    /// The result of a campaign attempt
    /// </summary>
    public class CampaignResponse
    {
        /// <summary>
        /// If the campaign resulted in this client being elected leader.
        /// </summary>
        public bool IsLeader { get; internal set; }

        /// <summary>
        /// The fencing token that is provided if elected leader.
        /// </summary>
        public HybridLogicalClock? FencingToken { get; internal set; }

        public CampaignResponse(bool isLeader, HybridLogicalClock? fencingToken)
        {
            IsLeader = isLeader;
            FencingToken = fencingToken;
        }
    }
}
