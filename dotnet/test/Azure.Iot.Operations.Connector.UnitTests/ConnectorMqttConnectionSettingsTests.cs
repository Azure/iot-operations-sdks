// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.ConnectorConfigurations;
using Azure.Iot.Operations.Protocol.Connection;
using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    // These tests rely on environment variables which may intefere with other similar tests
    [Collection("Environment Variable Sequential")]
    public class ConnectorMqttConnectionSettingsTests
    {
        public ConnectorMqttConnectionSettingsTests()
        {
            // Clear the environment variables before each test runs
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.ConnectorConfigMountPathEnvVar, null);
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.BrokerTrustBundleMountPathEnvVar, null);
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.BrokerSatMountPathEnvVar, null);
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.ConnectorClientIdEnvVar, null);
        }

        [Fact]
        public void TestConstructorWithAuthAndTls()
        {
            string expectedClientId = Guid.NewGuid().ToString();
            string expectedSatPath = Guid.NewGuid().ToString();

            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.ConnectorConfigMountPathEnvVar, "./connector-config");
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.BrokerTrustBundleMountPathEnvVar, "./trust-bundle");
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.BrokerSatMountPathEnvVar, expectedSatPath);
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.ConnectorClientIdEnvVar, expectedClientId);

            MqttConnectionSettings settings = ConnectorFileMountSettings.FromFileMount();

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

            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.ConnectorConfigMountPathEnvVar, "./connector-config-no-auth-no-tls");
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.ConnectorClientIdEnvVar, expectedClientId);

            MqttConnectionSettings settings = ConnectorFileMountSettings.FromFileMount();

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

            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.ConnectorClientIdEnvVar, expectedClientId);

            Assert.Throws<InvalidOperationException>(() => ConnectorFileMountSettings.FromFileMount());
        }

        [Fact]
        public void TestConstructorThrowsIfNoTlsConfiguredButNoCaCert()
        {
            string expectedClientId = Guid.NewGuid().ToString();

            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.ConnectorConfigMountPathEnvVar, "./connector-config");
            string emptyDirectoryPath = "./" + Guid.NewGuid().ToString();
            Directory.CreateDirectory(emptyDirectoryPath); // Create some empty directory
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.BrokerTrustBundleMountPathEnvVar, emptyDirectoryPath);
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.BrokerSatMountPathEnvVar, "someSatPath");
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.ConnectorClientIdEnvVar, expectedClientId);

            Assert.Throws<InvalidOperationException>(() => ConnectorFileMountSettings.FromFileMount());

            string nonExistantDirectoryPath = "./" + Guid.NewGuid().ToString();
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.BrokerTrustBundleMountPathEnvVar, nonExistantDirectoryPath);
            Assert.Throws<InvalidOperationException>(() => ConnectorFileMountSettings.FromFileMount());
        }
    }
}
