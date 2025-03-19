// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector
{
    public class ConnectorLeaderElectionConfiguration
    {
        public string LeadershipPositionId { get; set; }

        public TimeSpan LeadershipPositionTermLength { get; set; } = TimeSpan.FromSeconds(10);

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
