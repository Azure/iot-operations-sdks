
using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.LeaderElection
{
    public class CampaignResponse
    {
        public bool IsLeader { get; internal set; }

        public HybridLogicalClock? FencingToken { get; internal set; }

        internal CampaignResponse(bool isLeader, HybridLogicalClock? fencingToken)
        {
            IsLeader = isLeader;
            FencingToken = fencingToken;
        }
    }
}