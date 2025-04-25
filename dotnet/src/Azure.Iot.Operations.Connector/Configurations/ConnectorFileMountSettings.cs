// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Azure.Iot.Operations.Protocol.Connection;

namespace Azure.Iot.Operations.Connector.ConnectorConfigurations
{
    public class ConnectorFileMountSettings
    {
        public const string ConnectorConfigMountPathEnvVar = "CONNECTOR_CONFIGURATION_MOUNT_PATH";
        public const string BrokerTrustBundleMountPathEnvVar = "BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH";
        public const string BrokerSatMountPathEnvVar = "BROKER_SAT_MOUNT_PATH";
        public const string ConnectorClientIdEnvVar = "CONNECTOR_CLIENT_ID_PREFIX";

        public const string ConnectorMqttConfigFileName = "MQTT_CONNECTION_CONFIGURATION";
        public const string ConnectorAioMetadataFileName = "AIO_METADATA";
        public const string ConnectorDiagnosticsConfigFileName = "DIAGNOSTICS";

        /// <summary>
        /// Create an instance of <see cref="MqttConnectionSettings"/> using the files mounted when this connector was
        /// deployed.
        /// </summary>
        /// <returns>The instance of <see cref="MqttConnectionSettings"/> that allows the connector to connect to the MQTT broker.</returns>
        public static MqttConnectionSettings FromFileMount()
        {
            string clientId = Environment.GetEnvironmentVariable(ConnectorClientIdEnvVar) ?? "todo";
            string? brokerTrustBundleMountPath = Environment.GetEnvironmentVariable(BrokerTrustBundleMountPathEnvVar);
            string? brokerSatMountPath = Environment.GetEnvironmentVariable(BrokerSatMountPathEnvVar);

            ConnectorMqttConnectionConfiguration connectorMqttConfig = GetMqttConnectionConfiguration();

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
                throw new InvalidOperationException($"Could not parse the 'host' field into hostname and port. Expected format \"<hostname>:<port>\" but received {connectorMqttConfig.Host}");
            }

            bool useTls = false;
            X509Certificate2Collection chain = [];
            if (connectorMqttConfig.Tls != null
                && connectorMqttConfig.Tls.Mode != null
                && connectorMqttConfig.Tls.Mode.Equals("enabled", StringComparison.OrdinalIgnoreCase))
            {
                useTls = true;

                if (!string.IsNullOrWhiteSpace(brokerTrustBundleMountPath))
                {
                    if (!Directory.Exists(brokerTrustBundleMountPath))
                    {
                        throw new InvalidOperationException("Expected one or more files in trust bundle mount path, but the path was not found.");
                    }

                    bool atLeastOneCaFileFound = false;
                    foreach (string caFilePath in Directory.EnumerateFiles(brokerTrustBundleMountPath))
                    {
                        atLeastOneCaFileFound = true;
                        chain.ImportFromPemFile(caFilePath);
                    }

                    if (!atLeastOneCaFileFound)
                    {
                        throw new InvalidOperationException("Expected one or more files in trust bundle mount path, but none were found in the path.");
                    }
                }
            }

            var mqttConnectionSettings = new MqttConnectionSettings(hostname, clientId)
            {
                UseTls = useTls,
                SatAuthFile = brokerSatMountPath, // May be null if no SAT auth is used.
                TrustChain = chain,
                //ReceiveMaximum = connectorMqttConfig.MaxInflightMessages, //TODO: #697 needs to be released
                TcpPort = port
            };

            if (connectorMqttConfig.SessionExpirySeconds != null)
            {
                mqttConnectionSettings.SessionExpiry = TimeSpan.FromSeconds(connectorMqttConfig.SessionExpirySeconds.Value);
            }

            if (connectorMqttConfig.KeepAliveSeconds != null)
            {
                mqttConnectionSettings.KeepAlive = TimeSpan.FromSeconds(connectorMqttConfig.KeepAliveSeconds.Value);
            }

            return mqttConnectionSettings;
        }

        public static ConnectorDiagnostics GetConnectorDiagnostics()
        {
            string connectorConfigMountPath = Environment.GetEnvironmentVariable(ConnectorConfigMountPathEnvVar) ?? throw new InvalidOperationException($"Missing {ConnectorConfigMountPathEnvVar} environment variable");
            string connectorDiagnosticsConfigFileContents = File.ReadAllText(connectorConfigMountPath + "/" + ConnectorDiagnosticsConfigFileName) ?? throw new InvalidOperationException($"Missing {connectorConfigMountPath + "/" + ConnectorDiagnosticsConfigFileName} file");
            return JsonSerializer.Deserialize<ConnectorDiagnostics>(connectorDiagnosticsConfigFileContents) ?? throw new InvalidOperationException($"{connectorConfigMountPath + "/" + ConnectorDiagnosticsConfigFileName} file was empty");
        }

        public static AioMetadata GetAioMetadata()
        {
            string connectorConfigMountPath = Environment.GetEnvironmentVariable(ConnectorConfigMountPathEnvVar) ?? throw new InvalidOperationException($"Missing {ConnectorConfigMountPathEnvVar} environment variable");
            string connectorAioMetadataConfigFileContents = File.ReadAllText(connectorConfigMountPath + "/" + ConnectorAioMetadataFileName) ?? throw new InvalidOperationException($"Missing {connectorConfigMountPath + "/" + ConnectorAioMetadataFileName} file");
            return JsonSerializer.Deserialize<AioMetadata>(connectorAioMetadataConfigFileContents) ?? throw new InvalidOperationException($"{connectorConfigMountPath + "/" + ConnectorAioMetadataFileName} file was empty");
        }

        public static ConnectorMqttConnectionConfiguration GetMqttConnectionConfiguration()
        {
            string connectorConfigMountPath = Environment.GetEnvironmentVariable(ConnectorConfigMountPathEnvVar) ?? throw new InvalidOperationException($"Missing {ConnectorConfigMountPathEnvVar} environment variable");
            string? brokerTrustBundleMountPath = Environment.GetEnvironmentVariable(BrokerTrustBundleMountPathEnvVar);
            string? brokerSatMountPath = Environment.GetEnvironmentVariable(BrokerSatMountPathEnvVar);

            string connectorMqttConfigFileContents = File.ReadAllText(connectorConfigMountPath + "/" + ConnectorMqttConfigFileName) ?? throw new InvalidOperationException($"Missing {connectorConfigMountPath + "/" + ConnectorMqttConfigFileName} file");
            return JsonSerializer.Deserialize<ConnectorMqttConnectionConfiguration>(connectorMqttConfigFileContents) ?? throw new InvalidOperationException($"{connectorConfigMountPath + "/" + ConnectorMqttConfigFileName} file was empty");
        }

        private ConnectorFileMountSettings()
        {
            // Users won't construct this class
        }
    }
}
