// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Defines how a user passes in leader election information to a connector application.
    /// </summary>
    public interface IConnectorLeaderElectionConfigurationProvider
    {
        /// <summary>
        /// Get the leader election configuration to use in this connector.
        /// </summary>
        /// <returns>The leader election configuration to use in this connector</returns>
        ConnectorLeaderElectionConfiguration GetLeaderElectionConfiguration();
    }
}
