// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Azure.Iot.Operations.Protocol.Connection;

namespace Azure.Iot.Operations.Connector
{
    public class ConnectorMqttConnectionSettings
    {
        public const string ConnectorConfigMountPathEnvVar = "CONNECTOR_CONFIGURATION_MOUNT_PATH";
        public const string BrokerTrustBundleMountPathEnvVar = "BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH";
        public const string BrokerSatMountPathEnvVar = "BROKER_SAT_MOUNT_PATH";
        public const string ConnectorClientIdEnvVar = "CONNECTOR_CLIENT_ID";

        public const string ConnectorMqttConfigFileName = "MQTT_CONNECTION_CONFIGURATION";
        public const string ConnectorAioMetadataFileName = "AIO_METADATA";
        public const string ConnectorDiagnosticsConfigFileName = "DIAGNOSTICS";

        public static MqttConnectionSettings FromFileMount()
        {
            string clientId = Environment.GetEnvironmentVariable(ConnectorClientIdEnvVar) ?? throw new InvalidOperationException("Missing connector client id");
            string connectorConfigMountPath = Environment.GetEnvironmentVariable(ConnectorConfigMountPathEnvVar) ?? throw new InvalidOperationException("Missing configuration TODO");
            string? brokerTrustBundleMountPath = Environment.GetEnvironmentVariable(BrokerTrustBundleMountPathEnvVar);
            string? brokerSatMountPath = Environment.GetEnvironmentVariable(BrokerSatMountPathEnvVar);

            string connectorMqttConfigFileContents = File.ReadAllText(connectorConfigMountPath + "/" + ConnectorMqttConfigFileName) ?? throw new InvalidOperationException();
            MqttConnectionConfiguration connectorMqttConfig = JsonSerializer.Deserialize<MqttConnectionConfiguration>(connectorMqttConfigFileContents) ?? throw new InvalidOperationException();

            string connectorAioMetadataConfigFileContents = File.ReadAllText(connectorConfigMountPath + "/" + ConnectorAioMetadataFileName) ?? throw new InvalidOperationException();
            AioMetadata connectorAioMetadata = JsonSerializer.Deserialize<AioMetadata>(connectorAioMetadataConfigFileContents) ?? throw new InvalidOperationException();

            string connectorDiagnosticsConfigFileContents = File.ReadAllText(connectorConfigMountPath + "/" + ConnectorDiagnosticsConfigFileName) ?? throw new InvalidOperationException();
            ConnectorDiagnostics connectorDiagnosticsConfig = JsonSerializer.Deserialize<ConnectorDiagnostics>(connectorDiagnosticsConfigFileContents) ?? throw new InvalidOperationException();

            string hostname;
            int port;
            try
            {
                string[] hostParts = connectorMqttConfig.Host.Split(":");
                hostname = hostParts[0];
                port = int.Parse(hostParts[1], CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                throw new InvalidOperationException();
            }

            bool useTls = false;
            X509Certificate2Collection chain = [];
            if (connectorMqttConfig.Tls != null)
            {
                useTls = true;

                if (!string.IsNullOrWhiteSpace(brokerTrustBundleMountPath))
                {
                    if (!Directory.Exists(brokerTrustBundleMountPath))
                    {
                        throw new InvalidOperationException();
                    }

                    foreach (string caFilePath in Directory.EnumerateFiles(brokerTrustBundleMountPath))
                    {
                        chain.ImportFromPemFile(caFilePath);
                    }
                }
            }

            return new MqttConnectionSettings(connectorMqttConfig.Host, clientId)
            {
                UseTls = useTls,
                SatAuthFile = brokerSatMountPath, // May be null if no SAT auth is used.
                TrustChain = chain,
                SessionExpiry = TimeSpan.FromSeconds(connectorMqttConfig.SessionExpirySeconds),
                //TODO maxInFlight unused
                TcpPort = port
            };
        }
    }
}
