﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;

namespace SqlQualityAnalyzerConnectorApp
{
    public class LeaderElectionConfigurationProvider : IConnectorLeaderElectionConfigurationProvider
    {
        public static Func<IServiceProvider, IConnectorLeaderElectionConfigurationProvider> ConnectorLeaderElectionConfigurationProviderFactory = service =>
        {
            return new LeaderElectionConfigurationProvider();
        };

        public ConnectorLeaderElectionConfiguration GetLeaderElectionConfiguration()
        {
            return new("some-sql-leadership-position-id", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(9));
        }
    }
}
