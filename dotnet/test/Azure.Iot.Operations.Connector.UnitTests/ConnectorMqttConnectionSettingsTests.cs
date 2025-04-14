// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol.Connection;
using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public class ConnectorMqttConnectionSettingsTests
    {
        [Fact]
        public void TestConstructorWithAuthAndTls()
        {
            string expectedClientId = Guid.NewGuid().ToString();
            string expectedSatPath = Guid.NewGuid().ToString();

            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.ConnectorConfigMountPathEnvVar, "./TestMountFiles/ConnectorConfig");
            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.BrokerTrustBundleMountPathEnvVar, "./TestMountFiles/TrustBundle");
            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.BrokerSatMountPathEnvVar, expectedSatPath);
            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.ConnectorClientIdEnvVar, expectedClientId);

            MqttConnectionSettings settings = ConnectorMqttConnectionSettings.FromFileMount();

            Assert.Equal(expectedClientId, settings.ClientId);
            Assert.Equal(expectedSatPath, settings.SatAuthFile);
            Assert.Equal(TimeSpan.FromSeconds(20), settings.SessionExpiry);
            Assert.Equal(TimeSpan.FromSeconds(10), settings.KeepAlive);
            Assert.Equal("someHostName", settings.HostName);
            Assert.Equal(1234, settings.TcpPort);
            Assert.True(settings.UseTls);
            Assert.NotNull(settings.TrustChain);
            Assert.NotEmpty(settings.TrustChain);
        }

        [Fact]
        public void TestConstructorWithNoAuthAndNoTls()
        {
            string expectedClientId = Guid.NewGuid().ToString();
            string expectedSatPath = Guid.NewGuid().ToString();

            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.ConnectorConfigMountPathEnvVar, "./TestMountFiles/ConnectorConfigNoAuthNoTls");
            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.ConnectorClientIdEnvVar, expectedClientId);

            MqttConnectionSettings settings = ConnectorMqttConnectionSettings.FromFileMount();

            Assert.Equal(expectedClientId, settings.ClientId);
            Assert.Equal(TimeSpan.FromSeconds(20), settings.SessionExpiry);
            Assert.Equal(TimeSpan.FromSeconds(10), settings.KeepAlive);
            Assert.Equal("someHostName", settings.HostName);
            Assert.Equal(1234, settings.TcpPort);
            Assert.False(settings.UseTls);
            Assert.Null(settings.ClientCertificate);
            Assert.Null(settings.SatAuthFile); // fails when run in parallel?
            Assert.NotNull(settings.TrustChain);
            Assert.Empty(settings.TrustChain);
        }
    }
}
