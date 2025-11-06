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
        // Environment variable constants
        public const string ConnectorConfigMountPathEnvVar = "CONNECTOR_CONFIGURATION_MOUNT_PATH";
        public const string BrokerTrustBundleMountPathEnvVar = "BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH";
        public const string BrokerSatMountPathEnvVar = "BROKER_SAT_MOUNT_PATH";
        public const string ConnectorClientIdEnvVar = "CONNECTOR_ID";
        public const string AzureExtensionResourceIdEnvVar = "AZURE_EXTENSION_RESOURCEID";
        public const string ConnectorNamespaceEnvVar = "CONNECTOR_NAMESPACE";
        public const string ConnectorSecretsMetadataMountPathEnvVar = "CONNECTOR_SECRETS_METADATA_MOUNT_PATH";
        public const string ConnectorTrustSettingsMountPathEnvVar = "CONNECTOR_TRUST_SETTINGS_MOUNT_PATH";
        public const string DeviceEndpointTrustBundleMountPathEnvVar = "DEVICE_ENDPOINT_TLS_TRUST_BUNDLE_CA_CERT_MOUNT_PATH";
        public const string DeviceEndpointCredentialsMountPathEnvVar = "DEVICE_ENDPOINT_CREDENTIALS_MOUNT_PATH";

        // OTEL Stopgap environment variable constants - these will change in the future
        public const string OtlpGrpcMetricEndpointEnvVar = "OTLP_GRPC_METRIC_ENDPOINT";
        public const string OtlpGrpcLogEndpointEnvVar = "OTLP_GRPC_LOG_ENDPOINT";
        public const string OtlpGrpcTraceEndpointEnvVar = "OTLP_GRPC_TRACE_ENDPOINT";
        public const string FirstPartyGrpcMetricsCollectorCaPathEnvVar = "FIRST_PARTY_OTLP_GRPC_METRICS_COLLECTOR_CA_PATH";
        public const string FirstPartyGrpcLogCollectorCaPathEnvVar = "FIRST_PARTY_OTLP_GRPC_LOG_COLLECTOR_CA_PATH";
        public const string OtlpHttpMetricEndpointEnvVar = "OTLP_HTTP_METRIC_ENDPOINT";
        public const string OtlpHttpLogEndpointEnvVar = "OTLP_HTTP_LOG_ENDPOINT";
        public const string OtlpHttpTraceEndpointEnvVar = "OTLP_HTTP_TRACE_ENDPOINT";

        // File name constants
        public const string ConnectorMqttConfigFileName = "MQTT_CONNECTION_CONFIGURATION";
        public const string ConnectorAioMetadataFileName = "AIO_METADATA";
        public const string ConnectorDiagnosticsConfigFileName = "DIAGNOSTICS";
        public const string PersistentVolumeMountPathFileName = "PERSISTENT_VOLUME_MOUNT_PATH";
        public const string AdditionalConnectorConfigFileName = "ADDITIONAL_CONNECTOR_CONFIGURATION";

        /// <summary>
        /// Get the Azure Extension Resource ID from the environment.
        /// Rust property: azure_extension_resource_id
        /// </summary>
        /// <returns>The Azure Extension Resource ID.</returns>
        public static string GetAzureExtensionResourceId()
        {
            return Environment.GetEnvironmentVariable(AzureExtensionResourceIdEnvVar)
                ?? throw new InvalidOperationException($"Missing {AzureExtensionResourceIdEnvVar} environment variable");
        }

        /// <summary>
        /// Get the Connector ID from the environment.
        /// Rust property: connector_id
        /// </summary>
        /// <returns>The Connector ID.</returns>
        public static string GetConnectorId()
        {
            return Environment.GetEnvironmentVariable(ConnectorClientIdEnvVar)
                ?? throw new InvalidOperationException($"Missing {ConnectorClientIdEnvVar} environment variable");
        }

        /// <summary>
        /// Get the Connector Namespace from the environment.
        /// Rust property: connector_namespace
        /// </summary>
        /// <returns>The Connector Namespace.</returns>
        public static string GetConnectorNamespace()
        {
            return Environment.GetEnvironmentVariable(ConnectorNamespaceEnvVar)
                ?? throw new InvalidOperationException($"Missing {ConnectorNamespaceEnvVar} environment variable");
        }

        /// <summary>
        /// Get the Connector Secrets Metadata mount path from the environment.
        /// Rust property: connector_secrets_metadata_mount
        /// </summary>
        /// <returns>The path to the connector secrets metadata mount, or null if not configured.</returns>
        public static string? GetConnectorSecretsMetadataMountPath()
        {
            return Environment.GetEnvironmentVariable(ConnectorSecretsMetadataMountPathEnvVar);
        }

        /// <summary>
        /// Get the Connector Trust Settings mount path from the environment.
        /// Rust property: connector_trust_settings_mount
        /// </summary>
        /// <returns>The path to the connector trust settings mount, or null if not configured.</returns>
        public static string? GetConnectorTrustSettingsMountPath()
        {
            return Environment.GetEnvironmentVariable(ConnectorTrustSettingsMountPathEnvVar);
        }

        /// <summary>
        /// Get the Device Endpoint Trust Bundle mount path from the environment.
        /// Rust property: device_endpoint_trust_bundle_mount
        /// </summary>
        /// <returns>The path to the device endpoint trust bundle mount, or null if not configured.</returns>
        public static string? GetDeviceEndpointTrustBundleMountPath()
        {
            return Environment.GetEnvironmentVariable(DeviceEndpointTrustBundleMountPathEnvVar);
        }

        /// <summary>
        /// Get the Device Endpoint Credentials mount path from the environment.
        /// Rust property: device_endpoint_credentials_mount
        /// </summary>
        /// <returns>The path to the device endpoint credentials mount, or null if not configured.</returns>
        public static string? GetDeviceEndpointCredentialsMountPath()
        {
            return Environment.GetEnvironmentVariable(DeviceEndpointCredentialsMountPathEnvVar);
        }

        /// <summary>
        /// Get the list of persistent volumes from the connector configuration mount.
        /// Rust property: connector_configuration.persistent_volumes
        /// </summary>
        /// <returns>A list of persistent volume mount paths.</returns>
        public static List<string> GetPersistentVolumes()
        {
            string connectorConfigMountPath = Environment.GetEnvironmentVariable(ConnectorConfigMountPathEnvVar)
                ?? throw new InvalidOperationException($"Missing {ConnectorConfigMountPathEnvVar} environment variable");

            string persistentVolumesFilePath = Path.Combine(connectorConfigMountPath, PersistentVolumeMountPathFileName);

            if (!File.Exists(persistentVolumesFilePath))
            {
                return new List<string>();
            }

            return File.ReadAllLines(persistentVolumesFilePath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
        }

        /// <summary>
        /// Get the additional configuration JSON string from the connector configuration mount.
        /// Rust property: connector_configuration.additional_configuration
        /// </summary>
        /// <returns>The additional configuration as a JSON string, or null if not configured.</returns>
        public static string? GetAdditionalConfiguration()
        {
            string connectorConfigMountPath = Environment.GetEnvironmentVariable(ConnectorConfigMountPathEnvVar)
                ?? throw new InvalidOperationException($"Missing {ConnectorConfigMountPathEnvVar} environment variable");

            string additionalConfigFilePath = Path.Combine(connectorConfigMountPath, AdditionalConnectorConfigFileName);

            if (!File.Exists(additionalConfigFilePath))
            {
                return null;
            }

            return File.ReadAllText(additionalConfigFilePath);
        }

        /// <summary>
        /// Get the OTLP gRPC metric endpoint from the environment.
        /// Rust property: grpc_metric_endpoint
        /// </summary>
        /// <returns>The OTLP gRPC metric endpoint, or null if not configured.</returns>
        public static string? GetGrpcMetricEndpoint()
        {
            return Environment.GetEnvironmentVariable(OtlpGrpcMetricEndpointEnvVar);
        }

        /// <summary>
        /// Get the OTLP gRPC log endpoint from the environment.
        /// Rust property: grpc_log_endpoint
        /// </summary>
        /// <returns>The OTLP gRPC log endpoint, or null if not configured.</returns>
        public static string? GetGrpcLogEndpoint()
        {
            return Environment.GetEnvironmentVariable(OtlpGrpcLogEndpointEnvVar);
        }

        /// <summary>
        /// Get the OTLP gRPC trace endpoint from the environment.
        /// Rust property: grpc_trace_endpoint
        /// </summary>
        /// <returns>The OTLP gRPC trace endpoint, or null if not configured.</returns>
        public static string? GetGrpcTraceEndpoint()
        {
            return Environment.GetEnvironmentVariable(OtlpGrpcTraceEndpointEnvVar);
        }

        /// <summary>
        /// Get the first-party gRPC metrics collector CA mount path from the environment.
        /// Rust property: grpc_metric_collector_1p_ca_mount
        /// </summary>
        /// <returns>The path to the first-party gRPC metrics collector CA mount, or null if not configured.</returns>
        public static string? GetGrpcMetricCollector1pCaMount()
        {
            return Environment.GetEnvironmentVariable(FirstPartyGrpcMetricsCollectorCaPathEnvVar);
        }

        /// <summary>
        /// Get the first-party gRPC log collector CA mount path from the environment.
        /// Rust property: grpc_log_collector_1p_ca_mount
        /// </summary>
        /// <returns>The path to the first-party gRPC log collector CA mount, or null if not configured.</returns>
        public static string? GetGrpcLogCollector1pCaMount()
        {
            return Environment.GetEnvironmentVariable(FirstPartyGrpcLogCollectorCaPathEnvVar);
        }

        /// <summary>
        /// Get the OTLP HTTP metric endpoint from the environment.
        /// Rust property: http_metric_endpoint
        /// </summary>
        /// <returns>The OTLP HTTP metric endpoint, or null if not configured.</returns>
        public static string? GetHttpMetricEndpoint()
        {
            return Environment.GetEnvironmentVariable(OtlpHttpMetricEndpointEnvVar);
        }

        /// <summary>
        /// Get the OTLP HTTP log endpoint from the environment.
        /// Rust property: http_log_endpoint
        /// </summary>
        /// <returns>The OTLP HTTP log endpoint, or null if not configured.</returns>
        public static string? GetHttpLogEndpoint()
        {
            return Environment.GetEnvironmentVariable(OtlpHttpLogEndpointEnvVar);
        }

        /// <summary>
        /// Get the OTLP HTTP trace endpoint from the environment.
        /// Rust property: http_trace_endpoint
        /// </summary>
        /// <returns>The OTLP HTTP trace endpoint, or null if not configured.</returns>
        public static string? GetHttpTraceEndpoint()
        {
            return Environment.GetEnvironmentVariable(OtlpHttpTraceEndpointEnvVar);
        }

        /// <summary>
        /// Create an instance of <see cref="MqttConnectionSettings"/> using the files mounted when this connector was
        /// deployed.
        /// </summary>
        /// <returns>The instance of <see cref="MqttConnectionSettings"/> that allows the connector to connect to the MQTT broker.</returns>
        public static MqttConnectionSettings FromFileMount()
        {
            string clientId = Environment.GetEnvironmentVariable(ConnectorClientIdEnvVar) ?? throw new InvalidOperationException("No MQTT client Id configured by Akri operator");
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
                ReceiveMaximum = connectorMqttConfig.MaxInflightMessages,
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

        /// <summary>
        /// Get the Connector Diagnostics configuration from the connector configuration mount.
        /// Rust property: connector_configuration.diagnostics
        /// </summary>
        /// <returns>The Connector Diagnostics configuration.</returns>
        public static ConnectorDiagnostics GetConnectorDiagnostics()
        {
            string connectorConfigMountPath = Environment.GetEnvironmentVariable(ConnectorConfigMountPathEnvVar) ?? throw new InvalidOperationException($"Missing {ConnectorConfigMountPathEnvVar} environment variable");
            string connectorDiagnosticsConfigFileContents = File.ReadAllText(connectorConfigMountPath + "/" + ConnectorDiagnosticsConfigFileName) ?? throw new InvalidOperationException($"Missing {connectorConfigMountPath + "/" + ConnectorDiagnosticsConfigFileName} file");
            return JsonSerializer.Deserialize<ConnectorDiagnostics>(connectorDiagnosticsConfigFileContents) ?? throw new InvalidOperationException($"{connectorConfigMountPath + "/" + ConnectorDiagnosticsConfigFileName} file was empty");
        }

        /// <summary>
        /// Get the AIO Metadata from the connector configuration mount.
        /// </summary>
        /// <returns>The AIO Metadata.</returns>
        public static AioMetadata GetAioMetadata()
        {
            string connectorConfigMountPath = Environment.GetEnvironmentVariable(ConnectorConfigMountPathEnvVar) ?? throw new InvalidOperationException($"Missing {ConnectorConfigMountPathEnvVar} environment variable");
            string connectorAioMetadataConfigFileContents = File.ReadAllText(connectorConfigMountPath + "/" + ConnectorAioMetadataFileName) ?? throw new InvalidOperationException($"Missing {connectorConfigMountPath + "/" + ConnectorAioMetadataFileName} file");
            return JsonSerializer.Deserialize<AioMetadata>(connectorAioMetadataConfigFileContents) ?? throw new InvalidOperationException($"{connectorConfigMountPath + "/" + ConnectorAioMetadataFileName} file was empty");
        }

        /// <summary>
        /// Get the MQTT Connection Configuration from the connector configuration mount.
        /// Rust property: connector_configuration.mqtt_connection_configuration
        /// </summary>
        /// <returns>The MQTT Connection Configuration.</returns>
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
