using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Diagnostics;
using System.Security.Authentication;
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
            Assert.Null(cs.Password);
            Assert.Equal("", cs.ClientId);
            Assert.True(cs.CleanStart);
            Assert.Equal(TimeSpan.FromSeconds(60), cs.KeepAlive);
            Assert.Equal(TimeSpan.FromSeconds(3600), cs.SessionExpiry);
            Assert.Equal(TimeSpan.FromSeconds(30), cs.ConnectionTimeout);
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
                KeyFilePassword = "password",
                Password = "password",
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
            Assert.Equal("password", cs.Password);
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
                               "SessionExpiry=PT5M;" +
                               "Username=me;" +
                               "Password=password;" +
                               "CaFile=Connection/ca.txt;" +
                               "CertFile=Connection/TestSdkLiteCertPem.txt;" +
                               "KeyFile=Connection/TestSdkLiteCertKey.txt;" +
                               "UseTls=False;" +
                               "KeepAlive=PT2M;" +
                               "ConnectionTimeout=PT5S";

            MqttConnectionSettings cs = MqttConnectionSettings.FromConnectionString(connStr);
            Assert.Equal("localhost", cs.HostName);
            Assert.Equal(2323, cs.TcpPort);
            Assert.False(cs.UseTls);
            Assert.Equal("Connection/ca.txt", cs.CaFile);
            Assert.Equal("Connection/TestSdkLiteCertPem.txt", cs.CertFile);
            Assert.Equal("Connection/TestSdkLiteCertKey.txt", cs.KeyFile);
            Assert.Equal("me", cs.Username);
            Assert.Equal(TimeSpan.FromMinutes(2), cs.KeepAlive);
            Assert.Equal("password", cs.Password);
            Assert.Equal("clientid", cs.ClientId);
            Assert.False(cs.CleanStart);
            Assert.Equal(TimeSpan.FromMinutes(5), cs.SessionExpiry);
            Assert.NotNull(cs.ClientCertificate);
            Assert.Equal("CN=TestSdkLite", cs.ClientCertificate.Subject);
            Assert.Equal(TimeSpan.FromSeconds(5), cs.ConnectionTimeout);
        }

        [Fact]
        public void FromConnectionStringWithPasswordFile()
        {
            string connStr = "HostName=localhost;" +
                               "TcpPort=2323;" +
                               "ClientId=clientid;" +
                               "CleanStart=False;" +
                               "SessionExpiry=PT1H;" +
                               "Username=me;" +
                               "PasswordFile=mypassword.txt;" +
                               "CaFile=Connection/ca.txt;" +
                               "CertFile=Connection/TestSdkLiteCertPem.txt;" +
                               "KeyFile=Connection/TestSdkLiteCertKey.txt;" +
                               "UseTls=False;" +
                               "KeepAlive=PT2M";

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
                               "SessionExpiry=PT1H;" +
                               "SatAuthFile=my/token;" +
                               "CaFile=Connection/ca.txt;" +
                               "CertFile=Connection/TestSdkLiteCertPem.txt;" +
                               "KeyFile=Connection/TestSdkLiteCertKey.txt;" +
                               "UseTls=False;" +
                               "KeepAlive=PT2M";

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
            string connStr = "HostName=me;TcpPort=2323;SatAuthFile=my/token;Password=myPwd";
            var ex = Assert.Throws<AkriMqttException>(() => MqttConnectionSettings.FromConnectionString(connStr));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("SatAuthFile", ex.PropertyName);
            Assert.Equal("my/token", ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
            Assert.Equal("Invalid settings in provided Connection String: SatAuthFile cannot be used with Password or PasswordFile (Parameter 'SatAuthFile')", ex.Message);
        }

        [Fact]
        public void FromConnectionStringFailsWithBadSessionExpiry()
        {
            string connStr = "HostName=me;TcpPort=2323;SessionExpiry=200";
            var ex = Assert.Throws<AkriMqttException>(() => MqttConnectionSettings.FromConnectionString(connStr));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("SessionExpiry", ex.PropertyName);
            Assert.Equal("200", ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
            Assert.Equal("Invalid settings in provided Connection String: The string '200' is not a valid TimeSpan value. (Parameter 'SessionExpiry')", ex.Message);
        }

        [Fact]
        public void FromConnectionStringFailsWithBadConnectionTimeout()
        {
            string connStr = "HostName=me;TcpPort=2323;ConnectionTimeout=200";
            var ex = Assert.Throws<AkriMqttException>(() => MqttConnectionSettings.FromConnectionString(connStr));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("ConnectionTimeout", ex.PropertyName);
            Assert.Equal("200", ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
            Assert.Equal("Invalid settings in provided Connection String: The string '200' is not a valid TimeSpan value. (Parameter 'ConnectionTimeout')", ex.Message);
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
                KeyFilePassword = "sdklite"
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
                KeyFilePassword = "sdklite"
            };
            Assert.Equal("HostName=localhost;ClientId=clientId;CertFile=TestSdkLiteCertPwdPem.txt;KeyFile=***;KeyFilePassword=***;TcpPort=8883;CleanStart=True;SessionExpiry=PT1H;KeepAlive=PT1M;UseTls=True", mcs.ToString());
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
            string connStr = "HostName=foo;KeepAlive=12";
            var ex = Assert.Throws<AkriMqttException>(() => MqttConnectionSettings.FromConnectionString(connStr));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("KeepAlive", ex.PropertyName);
            Assert.Equal("12", ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
            Assert.Equal("Invalid settings in provided Connection String: The string '12' is not a valid TimeSpan value. (Parameter 'KeepAlive')", ex.Message);
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
            Assert.False(cs.CaRequireRevocationCheck);
            Assert.True(cs.CleanStart);
            Assert.Equal(TimeSpan.FromSeconds(45), cs.KeepAlive);
            Assert.Equal("clientId", cs.ClientId);
            Assert.Equal(TimeSpan.FromSeconds(60), cs.SessionExpiry);
            Assert.Equal(TimeSpan.FromSeconds(180), cs.ConnectionTimeout);
            Assert.Equal("username", cs.Username);
            Assert.Equal("password", cs.Password);
            Assert.Equal("Connection/ca.txt", cs.CaFile);
            Assert.Equal("Connection/TestSdkLiteCertPwd.txt", cs.PasswordFile);
            Assert.Equal("Connection/TestSdkLiteCertPwdPem.txt", cs.CertFile);
            Assert.Equal("Connection/TestSdkLiteCertPwdKey.txt", cs.KeyFile);
            Assert.Equal("sdklite", cs.KeyFilePassword);

            ResetEnvironmentVariables();
        }

        [Fact]
        public void LoadFromEnvVarsWithCertificateRevocationCheck()
        {
            ResetEnvironmentVariables();

            string envPath = "../../../Connection/testEnvFiles/validVarsWithCertificateRevocationCheck.txt";
            LoadEnvVarsFromFile(envPath);
            var cs = MqttConnectionSettings.FromEnvVars();

            Assert.True(cs.CaRequireRevocationCheck);

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
            Assert.False(cs.CaRequireRevocationCheck);
            Assert.True(cs.CleanStart);
            Assert.Equal(TimeSpan.FromSeconds(45), cs.KeepAlive);
            Assert.Equal("clientId", cs.ClientId);
            Assert.Equal(TimeSpan.FromSeconds(60), cs.SessionExpiry);
            Assert.Equal(TimeSpan.FromSeconds(180), cs.ConnectionTimeout);
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
            Assert.Null(cs.Password);
            Assert.Equal("", cs.ClientId);
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
            Assert.Equal("MQTT_HOST_NAME", ex.PropertyName);
            Assert.Equal(string.Empty, ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
            Assert.Equal("Invalid settings in provided Environment Variables: 'MQTT_HOST_NAME' is missing.", ex.Message);

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
            Assert.Equal("false", ex.PropertyValue);
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
                "MQTT_HOST_NAME",
                "MQTT_TCP_PORT",
                "MQTT_USE_TLS",
                "MQTT_CA_FILE",
                "MQTT_CA_REQUIRE_REVOCATION_CHECK",
                "MQTT_CLEAN_START",
                "MQTT_KEEP_ALIVE",
                "MQTT_CLIENT_ID",
                "MQTT_SESSION_EXPIRY",
                "MQTT_CONNECTION_TIMEOUT",
                "MQTT_USERNAME",
                "MQTT_PASSWORD",
                "MQTT_CERT_FILE",
                "MQTT_KEY_FILE",
                "MQTT_KEY_FILE_PASSWORD",
                "MQTT_SAT_AUTH_FILE"
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
            Assert.True(options.ChannelOptions.TlsOptions.ClientCertificatesProvider.GetCertificates().Count == 0);
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
            Assert.Empty(options.ChannelOptions.TlsOptions.ClientCertificatesProvider.GetCertificates());
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
            Assert.Empty(options.ChannelOptions.TlsOptions.ClientCertificatesProvider.GetCertificates());
            Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, options.ChannelOptions.TlsOptions.SslProtocol);
            Assert.Equal("user", options.Credentials.GetUserName(options));
            Assert.Equal("hello", Encoding.UTF8.GetString(options.Credentials.GetPassword(options)));
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
            Assert.Empty(options.ChannelOptions.TlsOptions.ClientCertificatesProvider.GetCertificates());
            Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, options.ChannelOptions.TlsOptions.SslProtocol);
            Assert.Equal("K8S-SAT", options.AuthenticationMethod);
            Assert.Equal("hello", Encoding.UTF8.GetString(options.AuthenticationData));
        }


        [Fact]
        public void BuildWithConnectionSettingsWithPassword()
        {
            MqttConnectionSettings mqttConnectionSettings = new("localhost")
            {
                ClientId = "clientId",
                TcpPort = 4343,
                Username = "user",
                Password = "mypassword",
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
            Assert.Empty(options.ChannelOptions.TlsOptions.ClientCertificatesProvider.GetCertificates());
            Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, options.ChannelOptions.TlsOptions.SslProtocol);
            Assert.Equal("user", options.Credentials.GetUserName(options));
            Assert.Equal("mypassword", Encoding.UTF8.GetString(options.Credentials.GetPassword(options)));
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
            Assert.NotEmpty(options.ChannelOptions.TlsOptions.ClientCertificatesProvider.GetCertificates());
            Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, options.ChannelOptions.TlsOptions.SslProtocol);
        }
    }
}