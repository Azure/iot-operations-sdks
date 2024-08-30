
using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.LeaderElection
{
    public class CampaignResponse
    {
        public bool IsLeader { get; internal set; }

        public LeaderElectionCandidate? LastKnownLeader { get; internal set; }

        public HybridLogicalClock? FencingToken { get; internal set; }

        internal CampaignResponse(bool isLeader, LeaderElectionCandidate? previousLeader, HybridLogicalClock? fencingToken)
        {
            IsLeader = isLeader;
            LastKnownLeader = previousLeader;
            FencingToken = fencingToken;
        }
    }
}