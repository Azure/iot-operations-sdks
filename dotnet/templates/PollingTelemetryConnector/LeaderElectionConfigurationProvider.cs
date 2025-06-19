﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;

namespace PollingTelemetryConnectorTemplate
{
    public class LeaderElectionConfigurationProvider : IConnectorLeaderElectionConfigurationProvider
    {
        public static Func<IServiceProvider, IConnectorLeaderElectionConfigurationProvider> Factory = service =>
        {
            return new LeaderElectionConfigurationProvider();
        };

        public ConnectorLeaderElectionConfiguration GetLeaderElectionConfiguration()
        {
            return new("some-http-leadership-position-id", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(9));
        }
    }
}
