// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public class ConnectorMqttConnectionSettingsTests
    {
        public ConnectorMqttConnectionSettingsTests()
        {
            // Clear the environment variables before each test runs
            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.ConnectorConfigMountPathEnvVar, null);
            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.BrokerTrustBundleMountPathEnvVar, null);
            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.BrokerSatMountPathEnvVar, null);
            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.ConnectorClientIdEnvVar, null);
        }

        [Fact]
        public void TestConstructorWithAuthAndTls()
        {
            string expectedClientId = Guid.NewGuid().ToString();
            string expectedSatPath = Guid.NewGuid().ToString();

            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.ConnectorConfigMountPathEnvVar, "./connector-config");
            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.BrokerTrustBundleMountPathEnvVar, "./trust-bundle");
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

            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.ConnectorConfigMountPathEnvVar, "./connector-config-no-auth-no-tls");
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

        [Fact]
        public void TestConstructorThrowsIfNoConnectorConfigEnvVarSet()
        {
            string expectedClientId = Guid.NewGuid().ToString();

            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.ConnectorClientIdEnvVar, expectedClientId);

            Assert.Throws<InvalidOperationException>(() => ConnectorMqttConnectionSettings.FromFileMount());
        }

        [Fact]
        public void TestConstructorThrowsIfNoTlsConfiguredButNoCaCert()
        {
            string expectedClientId = Guid.NewGuid().ToString();

            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.ConnectorConfigMountPathEnvVar, "./connector-config");
            string emptyDirectoryPath = "./" + Guid.NewGuid().ToString();
            Directory.CreateDirectory(emptyDirectoryPath); // Create some empty directory
            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.BrokerTrustBundleMountPathEnvVar, emptyDirectoryPath);
            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.BrokerSatMountPathEnvVar, "someSatPath");
            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.ConnectorClientIdEnvVar, expectedClientId);

            Assert.Throws<InvalidOperationException>(() => ConnectorMqttConnectionSettings.FromFileMount());

            string nonExistantDirectoryPath = "./" + Guid.NewGuid().ToString();
            Environment.SetEnvironmentVariable(ConnectorMqttConnectionSettings.BrokerTrustBundleMountPathEnvVar, nonExistantDirectoryPath);
            Assert.Throws<InvalidOperationException>(() => ConnectorMqttConnectionSettings.FromFileMount());
        }
    }
}
