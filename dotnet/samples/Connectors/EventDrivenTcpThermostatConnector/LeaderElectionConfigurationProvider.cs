// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;

namespace EventDrivenTcpThermostatConnector
{
    public class LeaderElectionConfigurationProvider : IConnectorLeaderElectionConfigurationProvider
    {
        public static Func<IServiceProvider, IConnectorLeaderElectionConfigurationProvider> ConnectorLeaderElectionConfigurationProviderFactory = service =>
        {
            return new LeaderElectionConfigurationProvider();
        };

        public ConnectorLeaderElectionConfiguration GetLeaderElectionConfiguration()
        {
            return new("some-tcp-leadership-position-id", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(9));
        }
    }
}
