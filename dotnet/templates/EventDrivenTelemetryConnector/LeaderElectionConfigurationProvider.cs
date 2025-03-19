// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;

namespace EventDrivenTelemetryConnector
{
    public class ConnectorLeaderElectionConfigurationProvider : IConnectorLeaderElectionConfigurationProvider
    {
        public static Func<IServiceProvider, IConnectorLeaderElectionConfigurationProvider> ConnectorLeaderElectionConfigurationProviderFactory = service =>
        {
            return new ConnectorLeaderElectionConfigurationProvider();
        };

        public ConnectorLeaderElectionConfiguration GetLeaderElectionConfiguration()
        {
            return new("some-http-leadership-position-id", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(9));
        }
    }
}
