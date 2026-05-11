// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;

namespace PollingTelemetryConnectorTemplate
{
    /// <summary>
    /// Factory that provides all leader election configurations for this connector.
    /// </summary>
    /// <remarks>
    /// Connectors can have active/passive replication by deploying more than one replica and having each replica
    /// provide the same leadership position Id. That way, only one replica is the leader at a time.
    ///
    /// If no replication of this connector is needed, then this class can be deleted.
    /// </remarks>
    public class LeaderElectionConfigurationProvider : IConnectorLeaderElectionConfigurationProvider
    {
        public static Func<IServiceProvider, IConnectorLeaderElectionConfigurationProvider> Factory = service =>
        {
            return new LeaderElectionConfigurationProvider();
        };

        public ConnectorLeaderElectionConfiguration GetLeaderElectionConfiguration()
        {
            // This value should be the same across any replicas but should be unique from connector to connector.
            string leadershipPositionId = "some-leadership-position-id";
            return new(leadershipPositionId, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(9));
        }
    }
}
