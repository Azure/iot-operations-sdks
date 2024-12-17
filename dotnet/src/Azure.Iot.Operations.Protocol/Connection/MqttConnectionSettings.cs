// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Azure.Iot.Operations.Protocol.Connection
{
    public class MqttConnectionSettings
    {
        private const int DefaultTcpPort = 8883;
        private const bool DefaultUseTls = true;
        private const bool DefaultCleanStart = true;

        private static readonly TimeSpan s_defaultKeepAlive = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan s_defaultSessionExpiry = TimeSpan.FromSeconds(3600);

        public string HostName { get; set; }

        public int TcpPort { get; set; } = DefaultTcpPort;

        public bool UseTls { get; set; } = DefaultUseTls;

        public string? CaFile { get; set; }
        public bool CleanStart { get; set; } = DefaultCleanStart;

        public TimeSpan KeepAlive { get; set; } = s_defaultKeepAlive;

        public string? ClientId { get; set; }

        public TimeSpan SessionExpiry { get; set; } = s_defaultSessionExpiry;

        public string? Username { get; set; }

        public string? PasswordFile { get; set; }

        public string? CertFile { get; set; }

        public string? KeyFile { get; set; }

        public string? KeyPasswordFile { get; set; }

        public X509Certificate2? ClientCertificate { get; set; }

        public X509Certificate2Collection? TrustChain { get; set; }

        public string? ModelId { get; set; }

        public string? SatAuthFile { get; set; }

        public MqttConnectionSettings(string hostname)
            : this(new Dictionary<string, string> { { nameof(HostName), hostname } }, false)
        {
        }

        protected MqttConnectionSettings(IDictionary<string, string> connectionSettings, bool validateOptionalSettings, bool isSettingFromConnStr = false)
        {
            try
            {
                HostName = GetStringValue(connectionSettings, nameof(HostName)) ?? string.Empty;
                ClientId = GetStringValue(connectionSettings, nameof(ClientId)) ?? string.Empty;
                CertFile = GetStringValue(connectionSettings, nameof(CertFile));
                KeyFile = GetStringValue(connectionSettings, nameof(KeyFile));
                KeyPasswordFile = GetStringValue(connectionSettings, nameof(KeyPasswordFile));
                Username = GetStringValue(connectionSettings, nameof(Username));
                PasswordFile = GetStringValue(connectionSettings, nameof(PasswordFile));
                ModelId = GetStringValue(connectionSettings, nameof(ModelId));
                KeepAlive = GetTimeSpanValue(connectionSettings, nameof(KeepAlive), s_defaultKeepAlive);
                CleanStart = GetBooleanValue(connectionSettings, nameof(CleanStart), DefaultCleanStart);
                SessionExpiry = GetTimeSpanValue(connectionSettings, nameof(SessionExpiry), s_defaultSessionExpiry);
                TcpPort = GetPositiveIntValueOrDefault(connectionSettings, nameof(TcpPort), DefaultTcpPort);
                UseTls = GetBooleanValue(connectionSettings, nameof(UseTls), DefaultUseTls);
                CaFile = GetStringValue(connectionSettings, nameof(CaFile));
                SatAuthFile = GetStringValue(connectionSettings, nameof(SatAuthFile));

                ValidateMqttSettings(validateOptionalSettings);
            }
            catch (ArgumentException ex)
            {
                Debug.Assert(ex.ParamName != null);
                _ = connectionSettings.TryGetValue(ex.ParamName, out string? paramValue);

                throw AkriMqttException.GetConfigurationInvalidException(
                    ex.ParamName,
                    paramValue ?? string.Empty,
                    isSettingFromConnStr ? "Invalid settings in provided Connection String: " + ex.Message : ex.Message,
                    ex);
            }
        }

        public static MqttConnectionSettings FromEnvVars()
        {
            string? hostname = Environment.GetEnvironmentVariable("AIO_BROKER_HOSTNAME");

            if (string.IsNullOrEmpty(hostname))
            {
                throw AkriMqttException.GetConfigurationInvalidException(
                    "AIO_BROKER_HOSTNAME",
                    string.Empty,
                    "Invalid settings in provided Environment Variables: 'AIO_BROKER_HOSTNAME' is missing.");
            }

            string? tcpPort = Environment.GetEnvironmentVariable("AIO_BROKER_TCP_PORT");
            string? clientId = Environment.GetEnvironmentVariable("AIO_MQTT_CLIENT_ID");
            string? certFile = Environment.GetEnvironmentVariable("AIO_TLS_CERT_FILE");
            string? keyFile = Environment.GetEnvironmentVariable("AIO_TLS_KEY_FILE");
            string? username = Environment.GetEnvironmentVariable("AIO_MQTT_USERNAME");
            string? passwordFile = Environment.GetEnvironmentVariable("AIO_MQTT_PASSWORD_FILE");
            string? keepAlive = Environment.GetEnvironmentVariable("AIO_MQTT_KEEP_ALIVE");
            string? sessionExpiry = Environment.GetEnvironmentVariable("AIO_MQTT_SESSION_EXPIRY");
            string? cleanStart = Environment.GetEnvironmentVariable("AIO_MQTT_CLEAN_START");
            string? useTls = Environment.GetEnvironmentVariable("AIO_MQTT_USE_TLS");
            string? caFile = Environment.GetEnvironmentVariable("AIO_TLS_CA_FILE");
            string? keyPasswordFile = Environment.GetEnvironmentVariable("AIO_TLS_KEY_PASSWORD_FILE");
            string? satAuthFile = Environment.GetEnvironmentVariable("AIO_SAT_FILE");

            try
            {
                return new MqttConnectionSettings(hostname)
                {
                    ClientId = clientId,
                    CertFile = certFile,
                    KeyFile = keyFile,
                    Username = username,
                    PasswordFile = passwordFile,
                    KeepAlive = string.IsNullOrEmpty(keepAlive) ? s_defaultKeepAlive : TimeSpan.FromSeconds(int.Parse(keepAlive, CultureInfo.InvariantCulture)),
                    SessionExpiry = string.IsNullOrEmpty(sessionExpiry) ? s_defaultSessionExpiry : TimeSpan.FromSeconds(int.Parse(sessionExpiry, CultureInfo.InvariantCulture)),
                    CleanStart = string.IsNullOrEmpty(cleanStart) || CheckForValidBooleanInput(nameof(CleanStart), cleanStart),
                    TcpPort = string.IsNullOrEmpty(tcpPort) ? DefaultTcpPort : CheckForValidIntegerInput(nameof(TcpPort), tcpPort),
                    UseTls = string.IsNullOrEmpty(useTls) || CheckForValidBooleanInput(nameof(UseTls), useTls),
                    CaFile = caFile,
                    KeyPasswordFile = string.IsNullOrEmpty(keyPasswordFile) ? null : File.ReadAllText(keyPasswordFile).Trim(),
                    SatAuthFile = satAuthFile
                };
            }
            catch (ArgumentException ex)
            {
                throw AkriMqttException.GetConfigurationInvalidException(ex.ParamName!, string.Empty, "Invalid settings in provided Environment Variables: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Construct an instance from the configuration files mounted by the Akri Operator.
        /// </summary>
        /// <remarks>
        /// This method is only usable for connector applications deployed as a kubernetes pod.
        /// </remarks>
        public static MqttConnectionSettings FromFileMount()
        {
            string configMapPath = Environment.GetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH")
                ?? throw new InvalidOperationException("AEP_CONFIGMAP_MOUNT_PATH is not set.");

            string? targetAddress;
            bool useTls;
            string? satMountPath = string.Empty;
            string? tlsCaCertMountPath = string.Empty;
            int port;

            try
            {
                string targetAddressAndPort = File.ReadAllText(configMapPath + "/BROKER_TARGET_ADDRESS");
                if (string.IsNullOrEmpty(targetAddressAndPort))
                {
                    throw new ArgumentException("BROKER_TARGET_ADDRESS is missing.");
                }

                try
                {
                    string[] targetAddressParts = targetAddressAndPort.Split(":");
                    targetAddress = targetAddressParts[0];
                    port = int.Parse(targetAddressParts[1], CultureInfo.InvariantCulture);
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"BROKER_TARGET_ADDRESS is malformed. Cannot parse MQTT port from BROKER_TARGET_ADDRESS. Expected format <hostname>:<port>. Found: {targetAddressAndPort}", e);
                }
            }
            catch (Exception ex)
            {
                throw AkriMqttException.GetConfigurationInvalidException("BROKER_TARGET_ADDRESS", string.Empty, "Missing or malformed target address configuration file", ex);
            }

            string? useTlsString = File.ReadAllText(configMapPath + "/BROKER_USE_TLS");
            if (string.IsNullOrWhiteSpace(useTlsString) || !bool.TryParse(useTlsString, out useTls))
            {
                throw AkriMqttException.GetConfigurationInvalidException("BROKER_USE_TLS", string.Empty, "BROKER_USE_TLS not set or contains a value that could not be parsed as a boolean.");
            }

            // Optional field, so no need to validate that this file exists
            satMountPath = Environment.GetEnvironmentVariable("BROKER_SAT_MOUNT_PATH");

            X509Certificate2Collection chain = [];
            tlsCaCertMountPath = Environment.GetEnvironmentVariable("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH");

            if (!string.IsNullOrWhiteSpace(tlsCaCertMountPath))
            {
                if (!Directory.Exists(tlsCaCertMountPath))
                {
                    throw AkriMqttException.GetConfigurationInvalidException("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH", string.Empty, "A TLS cert mount path was provided, but the provided path does not exist. Path: " + tlsCaCertMountPath);
                }

                foreach (string caFilePath in Directory.EnumerateFiles(tlsCaCertMountPath))
                {
                    chain.ImportFromPemFile(caFilePath);
                }
            }

            try
            {
                return new MqttConnectionSettings(targetAddress)
                {
                    UseTls = useTls,
                    SatAuthFile = satMountPath,
                    TrustChain = chain,
                    TcpPort = port
                };
            }
            catch (ArgumentException ex)
            {
                string? paramValue = ex.ParamName switch
                {
                    nameof(targetAddress) => targetAddress,
                    nameof(useTls) => useTls.ToString(),
                    nameof(satMountPath) => satMountPath,
                    nameof(tlsCaCertMountPath) => tlsCaCertMountPath,
                    _ => string.Empty
                };

                throw AkriMqttException.GetConfigurationInvalidException(ex.ParamName!, paramValue ?? string.Empty, "Invalid settings in provided configuration files: " + ex.Message, ex);
            }
        }

        public static MqttConnectionSettings FromConnectionString(string connectionString)
        {
            IDictionary<string, string> map = connectionString.ToDictionary(';', '=');
            return new MqttConnectionSettings(map, true, true);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly")]
        protected internal void ValidateMqttSettings(bool validateOptionalSettings)
        {
            if (string.IsNullOrWhiteSpace(HostName))
            {
                throw new ArgumentException($"{nameof(HostName)} is mandatory.", nameof(HostName));
            }

            if (string.IsNullOrEmpty(ClientId) && !CleanStart)
            {
                throw new ArgumentException($"{nameof(ClientId)} is mandatory when {nameof(CleanStart)} is set to false.", nameof(ClientId));
            }

            if (!string.IsNullOrEmpty(SatAuthFile) && (!string.IsNullOrEmpty(PasswordFile)))
            {
                throw new ArgumentException(
                    $"{nameof(SatAuthFile)} cannot be used with {nameof(PasswordFile)}", nameof(SatAuthFile));
            }

            if (validateOptionalSettings)
            {
                if (!string.IsNullOrWhiteSpace(KeyFile) && string.IsNullOrWhiteSpace(CertFile))
                {
                    throw new ArgumentException(
                        $"{nameof(CertFile)} and {nameof(KeyFile)} need to be provided together.",
                        $"{nameof(CertFile)} and {nameof(KeyFile)}");
                }
            }

            if (!string.IsNullOrWhiteSpace(CertFile))
            {
                ClientCertificate = string.IsNullOrWhiteSpace(KeyPasswordFile)
                    ? X509Certificate2.CreateFromPemFile(CertFile, KeyFile)
                    : X509Certificate2.CreateFromEncryptedPemFile(CertFile, KeyPasswordFile, KeyFile);
            }

            if (!string.IsNullOrWhiteSpace(CaFile))
            {
                X509Certificate2Collection chain = [];
                chain.ImportFromPemFile(CaFile);
                TrustChain = chain;
            }
        }

        protected static string? GetStringValue(IDictionary<string, string> dict, string propertyName)
        {
            string? result = default;
            if (dict.TryGetValue(propertyName, out string? value))
            {
                result = value;
            }
            return result;
        }

        protected static int GetPositiveIntValueOrDefault(IDictionary<string, string> dict, string propertyName, int defaultValue = default)
        {
            int result = defaultValue;
            if (dict.TryGetValue(propertyName, out string? stringValue))
            {
                try
                {
                    result = int.Parse(stringValue, CultureInfo.InvariantCulture);
                }
                catch (FormatException ex) // re-throw ex to ArgumentException in order to include the propertyName
                {
                    throw new ArgumentException(ex.Message, propertyName, ex);
                }
            }
            return result;
        }

        protected static bool GetBooleanValue(IDictionary<string, string> dict, string propertyName, bool defaultValue = default)
        {
            bool result = defaultValue;
            if (dict.TryGetValue(propertyName, out string? stringValue))
            {
                try
                {
                    result = bool.Parse(stringValue);
                }
                catch (FormatException ex) // re-throw ex to ArgumentException in order to include the propertyName
                {
                    throw new ArgumentException(ex.Message, propertyName, ex);
                }
            }
            return result;
        }

        protected static TimeSpan GetTimeSpanValue(IDictionary<string, string> dict, string propertyName, TimeSpan defaultValue = default)
        {
            TimeSpan result = defaultValue;
            if (dict.TryGetValue(propertyName, out string? stringValue))
            {
                try
                {
                    // Convert the string directly to a TimeSpan by interpreting it as seconds
                    int seconds = int.Parse(stringValue, CultureInfo.InvariantCulture);
                    result = TimeSpan.FromSeconds(seconds);
                }
                catch (FormatException ex)
                {
                    // Re-throw as ArgumentException to include the propertyName
                    throw new ArgumentException(ex.Message, propertyName, ex);
                }
                catch (OverflowException ex)
                {
                    throw new ArgumentException($"The value for {propertyName} is out of range for TimeSpan.", propertyName, ex);
                }
            }
            return result;
        }

        private static void AppendIfNotNullOrEmpty(StringBuilder sb, string name, string? val)
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                if (name.ToLower(CultureInfo.InvariantCulture).Contains("key", StringComparison.InvariantCulture)
                    || name.ToLower(CultureInfo.InvariantCulture).Contains("password", StringComparison.InvariantCulture))
                {
                    sb.Append(CultureInfo.InvariantCulture, $"{name}=***;");
                }
                else
                {
                    sb.Append(CultureInfo.InvariantCulture, $"{name}={val};");
                }
            }
        }

        private static int CheckForValidIntegerInput(string envVarName, string envVarValue)
        {
            return int.TryParse(envVarValue, out int result)
                ? result
                : throw new ArgumentException($"{envVarName}={envVarValue}. Expecting an integer value.", envVarName);
        }

        private static bool CheckForValidBooleanInput(string envVarName, string envVarValue)
        {
            return bool.TryParse(envVarValue, out bool result)
                ? result
                : throw new ArgumentException($"{envVarName}={envVarValue}. Expecting a boolean value.", envVarName);
        }

        public override string ToString()
        {
            StringBuilder result = new();
            AppendIfNotNullOrEmpty(result, nameof(HostName), HostName);
            AppendIfNotNullOrEmpty(result, nameof(ClientId), ClientId);
            AppendIfNotNullOrEmpty(result, nameof(ModelId), ModelId);
            AppendIfNotNullOrEmpty(result, nameof(Username), Username);
            AppendIfNotNullOrEmpty(result, nameof(PasswordFile), PasswordFile);
            AppendIfNotNullOrEmpty(result, nameof(CertFile), CertFile);
            AppendIfNotNullOrEmpty(result, nameof(KeyFile), KeyFile);
            AppendIfNotNullOrEmpty(result, nameof(KeyPasswordFile), KeyPasswordFile);
            AppendIfNotNullOrEmpty(result, nameof(TcpPort), TcpPort.ToString(CultureInfo.InvariantCulture));
            AppendIfNotNullOrEmpty(result, nameof(CleanStart), CleanStart.ToString());
            AppendIfNotNullOrEmpty(result, nameof(SessionExpiry), ((int)SessionExpiry.TotalSeconds).ToString(CultureInfo.InvariantCulture));
            AppendIfNotNullOrEmpty(result, nameof(KeepAlive), ((int)KeepAlive.TotalSeconds).ToString(CultureInfo.InvariantCulture));
            AppendIfNotNullOrEmpty(result, nameof(CaFile), CaFile);
            AppendIfNotNullOrEmpty(result, nameof(UseTls), UseTls.ToString());
            result.Remove(result.Length - 1, 1);
            return result.ToString();
        }
    }
}
