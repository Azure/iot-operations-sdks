// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Connector-level leader election configurations.
    /// </summary>
    public class ConnectorLeaderElectionConfiguration
    {
        /// <summary>
        /// The Id for the leadership position that this connector will campaign for. This value must be the same across any connector pods
        /// that want to passively replicate each other.
        /// </summary>
        public string LeadershipPositionId { get; set; }

        /// <summary>
        /// How long each leader will campaign to be leader for.
        /// </summary>
        /// <remarks>
        /// A leader will automatically attempt to renew its leadership position at an interval defined by <see cref="LeadershipPositionRenewalRate"/>.
        /// </remarks>
        public TimeSpan LeadershipPositionTermLength { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// How frequently the leader will attempt to renew its leadership position.
        /// </summary>
        /// <remarks>
        /// This value generally should be lower than <see cref="LeadershipPositionTermLength"/> so that the leader will renew its position prior to the previous position expiring.
        /// </remarks>
        public TimeSpan LeadershipPositionRenewalRate { get; set; } = TimeSpan.FromSeconds(9);

        public ConnectorLeaderElectionConfiguration(string leadershipPositionId, TimeSpan? leadershipPositionTermLength = null, TimeSpan? leadershipPositionRenewalRate = null)
        {
            LeadershipPositionId = leadershipPositionId;

            if (leadershipPositionTermLength != null)
            {
                LeadershipPositionTermLength = leadershipPositionTermLength.Value;
            }

            if (leadershipPositionRenewalRate != null)
            {
                LeadershipPositionRenewalRate = leadershipPositionRenewalRate.Value;
            }
        }
    }
}
