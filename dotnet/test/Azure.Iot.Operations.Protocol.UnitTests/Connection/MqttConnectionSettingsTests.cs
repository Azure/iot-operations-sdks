// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using System.Diagnostics;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Azure.Iot.Operations.Protocol.UnitTests.Connection
{
    public class MqttConnectionSettingsTests
    {
        [Fact]
        public void Defaults()
        {
            MqttConnectionSettings cs = new("TestHost");
            Assert.Equal("TestHost", cs.HostName);
            Assert.Equal(8883, cs.TcpPort);
            Assert.True(cs.UseTls);
            Assert.Null(cs.CaFile);
            Assert.Null(cs.CertFile);
            Assert.Null(cs.KeyFile);
            Assert.Null(cs.Username);
            Assert.Null(cs.PasswordFile);
            Assert.Equal("", cs.ClientId);
            Assert.True(cs.CleanStart);
            Assert.Equal(TimeSpan.FromSeconds(60), cs.KeepAlive);
            Assert.Equal(TimeSpan.FromSeconds(3600), cs.SessionExpiry);
            Assert.Null(cs.SatAuthFile);
        }

        [Fact]
        public void InitProps()
        {
            MqttConnectionSettings cs = new("localhost")
            {
                ClientId = "clientId",
                CaFile = "cafile",
                CleanStart = false,
                CertFile = "TestSdkLiteCertPem.txt",
                KeepAlive = TimeSpan.FromSeconds(23),
                KeyFile = "keyfile",
                KeyPasswordFile = "password",
                PasswordFile = "password.txt",
                TcpPort = 2323,
                Username = "me",
                UseTls = false
            };
            Assert.Equal("localhost", cs.HostName);
            Assert.Equal(2323, cs.TcpPort);
            Assert.False(cs.UseTls);
            Assert.Equal("cafile", cs.CaFile);
            Assert.Equal("TestSdkLiteCertPem.txt", cs.CertFile);
            Assert.Equal("keyfile", cs.KeyFile);
            Assert.Equal("me", cs.Username);
            Assert.Equal("password.txt", cs.PasswordFile);
            Assert.Equal("clientId", cs.ClientId);
            Assert.False(cs.CleanStart);
            Assert.Equal(TimeSpan.FromSeconds(23), cs.KeepAlive);
        }

        [Fact]
        public void FromConnectionString()
        {
            string connStr = "HostName=localhost;" +
                               "TcpPort=2323;" +
                               "ClientId=clientid;" +
                               "CleanStart=False;" +
                               "SessionExpiry=300;" +
                               "Username=me;" +
                               "PasswordFile=password.txt;" +
                               "CaFile=Connection/ca.txt;" +
                               "CertFile=Connection/TestSdkLiteCertPem.txt;" +
                               "KeyFile=Connection/TestSdkLiteCertKey.txt;" +
                               "UseTls=False;" +
                               "KeepAlive=120";

            MqttConnectionSettings cs = MqttConnectionSettings.FromConnectionString(connStr);
            Assert.Equal("localhost", cs.HostName);
            Assert.Equal(2323, cs.TcpPort);
            Assert.False(cs.UseTls);
            Assert.Equal("Connection/ca.txt", cs.CaFile);
            Assert.Equal("Connection/TestSdkLiteCertPem.txt", cs.CertFile);
            Assert.Equal("Connection/TestSdkLiteCertKey.txt", cs.KeyFile);
            Assert.Equal("me", cs.Username);
            Assert.Equal(TimeSpan.FromMinutes(2), cs.KeepAlive);
            Assert.Equal("password.txt", cs.PasswordFile);
            Assert.Equal("clientid", cs.ClientId);
            Assert.False(cs.CleanStart);
            Assert.Equal(TimeSpan.FromMinutes(5), cs.SessionExpiry);
            Assert.NotNull(cs.ClientCertificate);
            Assert.Equal("CN=TestSdkLite", cs.ClientCertificate.Subject);
        }

        [Fact]
        public void FromConnectionStringWithPasswordFile()
        {
            string connStr = "HostName=localhost;" +
                               "TcpPort=2323;" +
                               "ClientId=clientid;" +
                               "CleanStart=False;" +
                               "SessionExpiry=3600;" +
                               "Username=me;" +
                               "PasswordFile=mypassword.txt;" +
                               "CaFile=Connection/ca.txt;" +
                               "CertFile=Connection/TestSdkLiteCertPem.txt;" +
                               "KeyFile=Connection/TestSdkLiteCertKey.txt;" +
                               "UseTls=False;" +
                               "KeepAlive=120";

            MqttConnectionSettings cs = MqttConnectionSettings.FromConnectionString(connStr);
            Assert.Equal("localhost", cs.HostName);
            Assert.Equal(2323, cs.TcpPort);
            Assert.False(cs.UseTls);
            Assert.Equal("Connection/ca.txt", cs.CaFile);
            Assert.Equal("Connection/TestSdkLiteCertPem.txt", cs.CertFile);
            Assert.Equal("Connection/TestSdkLiteCertKey.txt", cs.KeyFile);
            Assert.Equal("me", cs.Username);
            Assert.Equal(TimeSpan.FromMinutes(2), cs.KeepAlive);
            Assert.Equal("mypassword.txt", cs.PasswordFile);
            Assert.Equal("clientid", cs.ClientId);
            Assert.False(cs.CleanStart);
            Assert.Equal(TimeSpan.FromHours(1), cs.SessionExpiry);
            Assert.NotNull(cs.ClientCertificate);
            Assert.Equal("CN=TestSdkLite", cs.ClientCertificate.Subject);
        }

        [Fact]
        public void FromConnectionStringWithSatAuthFile()
        {
            string connStr = "HostName=localhost;" +
                               "TcpPort=2323;" +
                               "ClientId=clientid;" +
                               "CleanStart=False;" +
                               "SessionExpiry=3600;" +
                               "SatAuthFile=my/token;" +
                               "CaFile=Connection/ca.txt;" +
                               "CertFile=Connection/TestSdkLiteCertPem.txt;" +
                               "KeyFile=Connection/TestSdkLiteCertKey.txt;" +
                               "UseTls=False;" +
                               "KeepAlive=120";

            MqttConnectionSettings cs = MqttConnectionSettings.FromConnectionString(connStr);
            Assert.Equal("localhost", cs.HostName);
            Assert.Equal(2323, cs.TcpPort);
            Assert.False(cs.UseTls);
            Assert.Equal("Connection/ca.txt", cs.CaFile);
            Assert.Equal("Connection/TestSdkLiteCertPem.txt", cs.CertFile);
            Assert.Equal("Connection/TestSdkLiteCertKey.txt", cs.KeyFile);
            Assert.Null(cs.Username);
            Assert.Equal(TimeSpan.FromMinutes(2), cs.KeepAlive);
            Assert.Equal("my/token", cs.SatAuthFile);
            Assert.Equal("clientid", cs.ClientId);
            Assert.False(cs.CleanStart);
            Assert.Equal(TimeSpan.FromHours(1), cs.SessionExpiry);
            Assert.NotNull(cs.ClientCertificate);
            Assert.Equal("CN=TestSdkLite", cs.ClientCertificate.Subject);
        }


        [Fact]
        public void FromConnectionStringFailsWithoutHostName()
        {
            string connStr = "TcpPort=2323";
            var ex = Assert.Throws<AkriMqttException>(() => MqttConnectionSettings.FromConnectionString(connStr));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("HostName", ex.PropertyName);
            Assert.Equal(string.Empty, ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
            Assert.Equal("Invalid settings in provided Connection String: HostName is mandatory. (Parameter 'HostName')", ex.Message);
        }

        [Fact]
        public void FromConnectionStringFailsWithSatAuthFileAndPassword()
        {
            string connStr = "HostName=me;TcpPort=2323;SatAuthFile=my/token;PasswordFile=Connection/TestSdkLitePwd.txt";
            var ex = Assert.Throws<AkriMqttException>(() => MqttConnectionSettings.FromConnectionString(connStr));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("SatAuthFile", ex.PropertyName);
            Assert.Equal("my/token", ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
            Assert.Equal("Invalid settings in provided Connection String: SatAuthFile cannot be used with PasswordFile (Parameter 'SatAuthFile')", ex.Message);
        }

        [Fact]
        public void FromConnectionStringFailsWithBadSessionExpiry()
        {
            string connStr = "HostName=me;TcpPort=2323;SessionExpiry=200secs";
            var ex = Assert.Throws<AkriMqttException>(() => MqttConnectionSettings.FromConnectionString(connStr));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("SessionExpiry", ex.PropertyName);
            Assert.Equal("200secs", ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
            Assert.Equal("Invalid settings in provided Connection String: The input string '200secs' was not in a correct format. (Parameter 'SessionExpiry')", ex.Message);
        }

        [Fact]
        public void CertFileSetsClientCertificate()
        {
            MqttConnectionSettings cs = new("localhost")
            {
                CertFile = "Connection/TestSdkLiteCertPem.txt",
                KeyFile = "Connection/TestSdkLiteCertKey.txt",
            };
            cs.ValidateMqttSettings(true);

            Assert.NotNull(cs.ClientCertificate);
            Assert.Equal("CN=TestSdkLite", cs.ClientCertificate.Subject);
        }

        [Fact]
        public void CertFileSetsClientCertificateWithPassword()
        {
            MqttConnectionSettings cs = new("localhost")
            {
                CertFile = "Connection/TestSdkLiteCertPwdPem.txt",
                KeyFile = "Connection/TestSdkLiteCertPwdKey.txt",
                KeyPasswordFile = "sdklite"
            };
            cs.ValidateMqttSettings(true);

            Assert.NotNull(cs.ClientCertificate);
            Assert.Equal("CN=sdklite", cs.ClientCertificate.Subject);
        }

        [Fact]
        public void CaFileSetsTrustChain()
        {
            MqttConnectionSettings cs = new("localhost")
            { 
                CaFile = "Connection/ca.txt"
            };
            cs.ValidateMqttSettings(true);

            Assert.NotNull(cs.TrustChain);
        }

        [Fact]
        public void ToStringReturnsConnectionString()
        {
            MqttConnectionSettings mcs = new("localhost")
            {
                ClientId="clientId",
                CertFile = "TestSdkLiteCertPwdPem.txt",
                KeyFile = "TestSdkLiteCertPwdKey.txt",
                KeyPasswordFile = "TestSdkLiteKeyPwd.txt"
            };
            Assert.Equal("HostName=localhost;ClientId=clientId;CertFile=TestSdkLiteCertPwdPem.txt;KeyFile=***;KeyPasswordFile=***;TcpPort=8883;CleanStart=True;SessionExpiry=3600;KeepAlive=60;UseTls=True", mcs.ToString());
        }

        [Fact]
        public void KeyFileWithoutCertFileFails()
        {
            MqttConnectionSettings cs = new("localhost")
            {
                KeyFile = "TestSdkLiteCertKey.txt",
            };
            var ex = Assert.Throws<ArgumentException>(() => cs.ValidateMqttSettings(true));
            Assert.Equal("CertFile and KeyFile", ex.ParamName);
            Assert.Equal("CertFile and KeyFile need to be provided together. (Parameter 'CertFile and KeyFile')", ex.Message);
        }

        [Fact]
        public void FromConnectionStringWithKeyFileWithoutCertFileFails()
        {
            string connStr = "HostName=localhost;KeyFile=TestSdkLiteCertKey.txt";
            var ex = Assert.Throws<AkriMqttException>(() => MqttConnectionSettings.FromConnectionString(connStr));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("CertFile and KeyFile", ex.PropertyName);
            Assert.Equal(string.Empty, ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
            Assert.Equal("Invalid settings in provided Connection String: CertFile and KeyFile need to be provided together. (Parameter 'CertFile and KeyFile')", ex.Message);
        }

        [Fact]
        public void FromConnectionStringWithInvalidTimeSpanThrowsException()
        {
            string connStr = "HostName=foo;KeepAlive=12min";
            var ex = Assert.Throws<AkriMqttException>(() => MqttConnectionSettings.FromConnectionString(connStr));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("KeepAlive", ex.PropertyName);
            Assert.Equal("12min", ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
            Assert.Equal("Invalid settings in provided Connection String: The input string '12min' was not in a correct format. (Parameter 'KeepAlive')", ex.Message);
        }

        [Fact]
        public void FromConnectionStringWithInvalidIntegerThrowsException()
        {
            string connStr = "HostName=foo;TcpPort=something";
            var ex = Assert.Throws<AkriMqttException>(() => MqttConnectionSettings.FromConnectionString(connStr));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("TcpPort", ex.PropertyName);
            Assert.Equal("something", ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
            Assert.Equal("Invalid settings in provided Connection String: The input string 'something' was not in a correct format. (Parameter 'TcpPort')", ex.Message);
        }

        [Fact]
        public void FromConnectionStringWithInvalidBooleanThrowsException()
        {
            string connStr = "HostName=foo;CleanStart=something";
            var ex = Assert.Throws<AkriMqttException>(() => MqttConnectionSettings.FromConnectionString(connStr));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("CleanStart", ex.PropertyName);
            Assert.Equal("something", ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
            Assert.Equal("Invalid settings in provided Connection String: String 'something' was not recognized as a valid Boolean. (Parameter 'CleanStart')", ex.Message);
        }


        [Fact]
        public void LoadFromEnvVarsAllVariables()
        {
            ResetEnvironmentVariables();

            string envPath = "../../../Connection/testEnvFiles/validVars.txt";
            LoadEnvVarsFromFile(envPath);
            var cs = MqttConnectionSettings.FromEnvVars();

            Assert.NotNull(cs.HostName);
            Assert.Equal("localhost", cs.HostName);
            Assert.Equal(8883, cs.TcpPort);
            Assert.True(cs.UseTls);
            Assert.True(cs.CleanStart);
            Assert.Equal(TimeSpan.FromSeconds(45), cs.KeepAlive);
            Assert.Equal("clientId", cs.ClientId);
            Assert.Equal(TimeSpan.FromSeconds(60), cs.SessionExpiry);
            Assert.Equal("username", cs.Username);
            Assert.Equal("Connection/TestSdkLitePwd.txt", cs.PasswordFile);
            Assert.Equal("Connection/ca.txt", cs.CaFile);
            Assert.Equal("Connection/TestSdkLiteCertPwdPem.txt", cs.CertFile);
            Assert.Equal("Connection/TestSdkLiteCertPwdKey.txt", cs.KeyFile);
            Assert.Equal("sdklite", cs.KeyPasswordFile);

            ResetEnvironmentVariables();
        }

        [Fact]
        public void LoadFromEnvVarsWithSat()
        {
            ResetEnvironmentVariables();

            string envPath = "../../../Connection/testEnvFiles/validVarsSat.txt";
            LoadEnvVarsFromFile(envPath);
            var cs = MqttConnectionSettings.FromEnvVars();

            Assert.NotNull(cs.HostName);
            Assert.Equal("localhost", cs.HostName);
            Assert.Equal(8883, cs.TcpPort);
            Assert.True(cs.UseTls);
            Assert.True(cs.CleanStart);
            Assert.Equal(TimeSpan.FromSeconds(45), cs.KeepAlive);
            Assert.Equal("clientId", cs.ClientId);
            Assert.Equal(TimeSpan.FromSeconds(60), cs.SessionExpiry);
            Assert.Equal("Connection/ca.txt", cs.CaFile);
            Assert.Equal("my/token", cs.SatAuthFile);
            ResetEnvironmentVariables();
        }

        [Fact]
        public void LoadFromEnvVarsDefauts()
        {
            ResetEnvironmentVariables();

            string envPath = "../../../Connection/testEnvFiles/defaults.txt";
            LoadEnvVarsFromFile(envPath);
            var cs = MqttConnectionSettings.FromEnvVars();
            cs.ValidateMqttSettings(true);

            Assert.Equal("localhost", cs.HostName);
            Assert.Equal(8883, cs.TcpPort);
            Assert.True(cs.UseTls);
            Assert.Null(cs.CaFile);
            Assert.Null(cs.CertFile);
            Assert.Null(cs.KeyFile);
            Assert.Null(cs.Username);
            Assert.Null(cs.PasswordFile);
            Assert.Null(cs.ClientId);
            Assert.Equal(TimeSpan.FromSeconds(3600), cs.SessionExpiry);
            Assert.True(cs.CleanStart);
            Assert.Equal(TimeSpan.FromSeconds(60), cs.KeepAlive);

            ResetEnvironmentVariables();
        }

        [Fact]
        public void LoadFromEnvVarsMissingHostname()
        {
            ResetEnvironmentVariables();

            string envPath = "../../../Connection/testEnvFiles/missingHostName.txt";
            LoadEnvVarsFromFile(envPath);
            var ex = Assert.Throws<AkriMqttException>(MqttConnectionSettings.FromEnvVars);
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("AIO_BROKER_HOSTNAME", ex.PropertyName);
            Assert.Equal(string.Empty, ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
            Assert.Equal("Invalid settings in provided Environment Variables: 'AIO_BROKER_HOSTNAME' is missing.", ex.Message);

            ResetEnvironmentVariables();
        }

        [Fact]
        public void LoadFromEnvVarsInvalidInputs()
        {
            ResetEnvironmentVariables();

            string envPath = "../../../Connection/testEnvFiles/invalidInput.txt";
            LoadEnvVarsFromFile(envPath);
            var ex = Assert.Throws<AkriMqttException>(MqttConnectionSettings.FromEnvVars);
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("TcpPort", ex.PropertyName);
            Assert.Null(ex.CorrelationId);
            Assert.Equal("Invalid settings in provided Environment Variables: TcpPort=false. Expecting an integer value. (Parameter 'TcpPort')", ex.Message);

            ResetEnvironmentVariables();
        }

        private static void LoadEnvVarsFromFile(string envVarFilePath)
        {
            ResetEnvironmentVariables();

            if (string.IsNullOrEmpty(envVarFilePath))
            {
                envVarFilePath = ".env";
            }

            if (File.Exists(envVarFilePath))
            {
                Trace.TraceInformation("Loading environment variables from {envFile}" + new FileInfo(envVarFilePath).FullName);
                foreach (string line in File.ReadAllLines(envVarFilePath))
                {
                    string[] parts = line.Split('=', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                }
            }
            else
            {
                Trace.TraceWarning($"EnvFile Not found in path {new DirectoryInfo(".").FullName} {envVarFilePath}");
            }
        }

        private static void ResetEnvironmentVariables()
        {
            var possibleSetVariables = new List<string>()
            {
                "AIO_BROKER_HOSTNAME",
                "AIO_BROKER_TCP_PORT",
                "AIO_MQTT_USE_TLS",
                "AIO_TLS_CA_FILE",
                "AIO_MQTT_CLEAN_START",
                "AIO_MQTT_KEEP_ALIVE",
                "AIO_MQTT_CLIENT_ID",
                "AIO_MQTT_SESSION_EXPIRY",
                "AIO_MQTT_USERNAME",
                "AIO_MQTT_PASSWORD_FILE",
                "AIO_TLS_CERT_FILE",
                "AIO_TLS_KEY_FILE",
                "AIO_TLS_KEY_PASSWORD_FILE",
                "AIO_SAT_FILE"
            };

            foreach (string envVar in possibleSetVariables)
            {
                Environment.SetEnvironmentVariable(envVar, null);
            }
        }

        [Fact]
        public void BuildWithConnectionSettings()
        {
            MqttConnectionSettings mqttConnectionSettings = new("localhost")
            {
                ClientId = "clientId",
                TcpPort = 4343,
                Username = "user",
                KeepAlive = TimeSpan.FromSeconds(15),
                CleanStart = false,
            };
            MqttClientOptions options = new(mqttConnectionSettings);
            Assert.NotNull(options);
            Assert.False(options.CleanSession);
            Assert.Equal("clientId", options.ClientId);
            Assert.Equal(MqttProtocolVersion.V500, options.ProtocolVersion);
            Assert.Equal(TimeSpan.FromSeconds(15), options.KeepAlivePeriod);
            MqttClientTcpOptions tcpOptions = (MqttClientTcpOptions)options.ChannelOptions;
            Assert.Equal("localhost", tcpOptions.Host);
            Assert.Equal(4343, tcpOptions.Port);
            Assert.Equal(3600, (double)options.SessionExpiryInterval);
            Assert.True(options.ChannelOptions.TlsOptions.UseTls);
            Assert.True(options.ChannelOptions.TlsOptions.ClientCertificatesProvider!.GetCertificates().Count == 0);
            Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, options.ChannelOptions.TlsOptions.SslProtocol);
        }

        [Fact]
        public void BuildWithConnectionSettingsSetsTrustChainCorrectly()
        {
            MqttConnectionSettings mqttConnectionSettings = new("localhost")
            {
                ClientId = "clientId",
                TcpPort = 4343,
                Username = "user",
                KeepAlive = TimeSpan.FromSeconds(15),
                CaFile = "Connection/ca.txt",
            };
            MqttClientOptions options = new(mqttConnectionSettings);
            Assert.NotNull(options);
            Assert.True(options.CleanSession);
            Assert.Equal("clientId", options.ClientId);
            Assert.Equal(MqttProtocolVersion.V500, options.ProtocolVersion);
            Assert.Equal(TimeSpan.FromSeconds(15), options.KeepAlivePeriod);
            MqttClientTcpOptions tcpOptions = (MqttClientTcpOptions)options.ChannelOptions;
            Assert.Equal("localhost", tcpOptions.Host);
            Assert.Equal(4343, tcpOptions.Port);
            Assert.Equal(3600, (double)options.SessionExpiryInterval);
            Assert.True(options.ChannelOptions.TlsOptions.UseTls);
            Assert.Empty(options.ChannelOptions.TlsOptions.ClientCertificatesProvider!.GetCertificates());
            Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, options.ChannelOptions.TlsOptions.SslProtocol);
        }


        [Fact]
        public void BuildWithConnectionSettingsWithPasswordFile()
        {
            MqttConnectionSettings mqttConnectionSettings = new("localhost")
            {
                ClientId = "clientId",
                TcpPort = 4343,
                Username = "user",
                PasswordFile = "Connection/mypassword.txt",
                KeepAlive = TimeSpan.FromSeconds(15),
                CaFile = "Connection/ca.txt",
            };
            MqttClientOptions options = new(mqttConnectionSettings);
            Assert.NotNull(options);
            Assert.True(options.CleanSession);
            Assert.Equal("clientId", options.ClientId);
            Assert.Equal(MqttProtocolVersion.V500, options.ProtocolVersion);
            Assert.Equal(TimeSpan.FromSeconds(15), options.KeepAlivePeriod);
            MqttClientTcpOptions tcpOptions = (MqttClientTcpOptions)options.ChannelOptions;
            Assert.Equal("localhost", tcpOptions.Host);
            Assert.Equal(4343, tcpOptions.Port);
            Assert.Equal(3600, (double)options.SessionExpiryInterval);
            Assert.True(options.ChannelOptions.TlsOptions.UseTls);
            Assert.Empty(options.ChannelOptions.TlsOptions.ClientCertificatesProvider!.GetCertificates());
            Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, options.ChannelOptions.TlsOptions.SslProtocol);
            Assert.Equal("user", options.Credentials!.GetUserName(options));
            Assert.Equal("hello", Encoding.UTF8.GetString(options.Credentials!.GetPassword(options)!));
        }

        [Fact]
        public void BuildWithConnectionSettingsWithSatAuthFile()
        {
            MqttConnectionSettings mqttConnectionSettings = new("localhost")
            {
                ClientId = "clientId",
                TcpPort = 4343,
                SatAuthFile = "Connection/mypassword.txt",
                KeepAlive = TimeSpan.FromSeconds(15),
                CaFile = "Connection/ca.txt",
            };
            MqttClientOptions options = new(mqttConnectionSettings);
            Assert.NotNull(options);
            Assert.True(options.CleanSession);
            Assert.Equal("clientId", options.ClientId);
            Assert.Equal(MqttProtocolVersion.V500, options.ProtocolVersion);
            Assert.Equal(TimeSpan.FromSeconds(15), options.KeepAlivePeriod);
            MqttClientTcpOptions tcpOptions = (MqttClientTcpOptions)options.ChannelOptions;
            Assert.Equal("localhost", tcpOptions.Host);
            Assert.Equal(4343, tcpOptions.Port);
            Assert.Equal(3600, (double)options.SessionExpiryInterval);
            Assert.True(options.ChannelOptions.TlsOptions.UseTls);
            Assert.Empty(options.ChannelOptions.TlsOptions.ClientCertificatesProvider!.GetCertificates());
            Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, options.ChannelOptions.TlsOptions.SslProtocol);
            Assert.Equal("K8S-SAT", options.AuthenticationMethod);
            Assert.Equal("hello", Encoding.UTF8.GetString(options.AuthenticationData!));
        }


        [Fact]
        public void BuildWithConnectionSettingsWithPassword()
        {
            MqttConnectionSettings mqttConnectionSettings = new("localhost")
            {
                ClientId = "clientId",
                TcpPort = 4343,
                Username = "user",
                PasswordFile = "Connection/TestSdkLitePwd.txt",
                KeepAlive = TimeSpan.FromSeconds(15),
                CaFile = "Connection/ca.txt",
            };
            MqttClientOptions options = new(mqttConnectionSettings);
            Assert.NotNull(options);
            Assert.True(options.CleanSession);
            Assert.Equal("clientId", options.ClientId);
            Assert.Equal(MqttProtocolVersion.V500, options.ProtocolVersion);
            Assert.Equal(TimeSpan.FromSeconds(15), options.KeepAlivePeriod);
            MqttClientTcpOptions tcpOptions = (MqttClientTcpOptions)options.ChannelOptions;
            Assert.Equal("localhost", tcpOptions.Host);
            Assert.Equal(4343, tcpOptions.Port);
            Assert.Equal(3600, (double)options.SessionExpiryInterval);
            Assert.True(options.ChannelOptions.TlsOptions.UseTls);
            Assert.Empty(options.ChannelOptions.TlsOptions.ClientCertificatesProvider!.GetCertificates());
            Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, options.ChannelOptions.TlsOptions.SslProtocol);
            Assert.Equal("user", options.Credentials!.GetUserName(options));
            Assert.Equal("password", Encoding.UTF8.GetString(options.Credentials.GetPassword(options)!).Trim('\uFEFF'));
        }


        [Fact]
        public void BuildWithClientCertificates()
        {
            MqttConnectionSettings mqttConnectionSettings = new("localhost")
            {
                ClientId = "clientId",
                TcpPort = 4343,
                CertFile = "Connection/TestSdkLiteCertPem.txt",
                KeyFile = "Connection/TestSdkLiteCertKey.txt",
                KeepAlive = TimeSpan.FromSeconds(15),
                CaFile = "Connection/ca.txt",
            };
            MqttClientOptions options = new(mqttConnectionSettings);
            Assert.NotNull(options);
            Assert.True(options.CleanSession);
            Assert.Equal("clientId", options.ClientId);
            Assert.Equal(MqttProtocolVersion.V500, options.ProtocolVersion);
            Assert.Equal(TimeSpan.FromSeconds(15), options.KeepAlivePeriod);
            MqttClientTcpOptions tcpOptions = (MqttClientTcpOptions)options.ChannelOptions;
            Assert.Equal("localhost", tcpOptions.Host);
            Assert.Equal(4343, tcpOptions.Port);
            Assert.True(options.ChannelOptions.TlsOptions.UseTls);
            Assert.NotEmpty(options.ChannelOptions.TlsOptions.ClientCertificatesProvider!.GetCertificates());
            Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, options.ChannelOptions.TlsOptions.SslProtocol);
        }


        [Fact]
        public void BuildWithConnectionSettingsWithCaTrustChain()
        {
            X509Certificate2Collection expectedTrustChain = new();
            expectedTrustChain.ImportFromPemFile("Connection/ca.txt");

            MqttConnectionSettings mqttConnectionSettings = new("localhost")
            {
                ClientId = "clientId",
                TcpPort = 4343,
                Username = "user",
                KeepAlive = TimeSpan.FromSeconds(15),
                TrustChain = expectedTrustChain,
            };
            MqttClientOptions options = new(mqttConnectionSettings);
            Assert.NotNull(options);
            Assert.True(options.CleanSession);
            Assert.Equal("clientId", options.ClientId);
            Assert.Equal(MqttProtocolVersion.V500, options.ProtocolVersion);
            Assert.Equal(TimeSpan.FromSeconds(15), options.KeepAlivePeriod);
            MqttClientTcpOptions tcpOptions = (MqttClientTcpOptions)options.ChannelOptions;
            Assert.Equal("localhost", tcpOptions.Host);
            Assert.Equal(4343, tcpOptions.Port);
            Assert.Equal(3600, (double)options.SessionExpiryInterval);
            Assert.True(options.ChannelOptions.TlsOptions.UseTls);
            Assert.Empty(options.ChannelOptions.TlsOptions.ClientCertificatesProvider!.GetCertificates());
            Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, options.ChannelOptions.TlsOptions.SslProtocol);
            Assert.Equal(expectedTrustChain, options.ChannelOptions.TlsOptions.TrustChain);
        }

        [Fact]
        public void CreateFromFileMount()
        {
            var expected = new MqttConnectionSettings("somehostname")
            {
                SatAuthFile = "sat.txt",
                TcpPort = 1234,
                UseTls = false,
            };

            // This makes the connection settings read the CaFile and build the trust chain for later comparison
            expected.ValidateMqttSettings(true);

            Environment.SetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH", "./Connection/testMountFiles");
            Environment.SetEnvironmentVariable("BROKER_SAT_MOUNT_PATH", "sat.txt");
            var actual = MqttConnectionSettings.FromFileMount();

            Assert.Equal(expected.HostName, actual.HostName);
            Assert.Equal(expected.UseTls, actual.UseTls);
            Assert.Equal(expected.TcpPort, actual.TcpPort);
            Assert.Equal(expected.SatAuthFile, actual.SatAuthFile);
        }

        [Fact]
        public void CreateFromFileMount_ThrowsIfMisconfiguredTrustedCertsDirectory()
        {
            Environment.SetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH", "./Connection/testMountFiles");
            Environment.SetEnvironmentVariable("BROKER_SAT_MOUNT_PATH", "sat.txt");
            Environment.SetEnvironmentVariable("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH", "thisDirectory/does/not/exist");
            Assert.Throws<AkriMqttException>(() => MqttConnectionSettings.FromFileMount());
        }
    }
}