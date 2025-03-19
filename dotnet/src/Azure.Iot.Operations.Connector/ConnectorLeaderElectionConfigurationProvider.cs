// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector
{
    public class ConnectorLeaderElectionConfigurationProvider : IConnectorLeaderElectionConfigurationProvider
    {
        private readonly ConnectorLeaderElectionConfiguration _configuration;

        public ConnectorLeaderElectionConfigurationProvider(ConnectorLeaderElectionConfiguration configuration)
        {
            _configuration = configuration;
        }

        public ConnectorLeaderElectionConfiguration GetLeaderElectionConfiguration()
        {
            return _configuration;
        }
    }
}
