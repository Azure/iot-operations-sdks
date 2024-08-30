using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace Azure.Iot.Operations.Protocol.Connection;

public class MqttConnectionSettings
{
    private const int DefaultTcpPort = 8883;
    private const bool DefaultUseTls = true;
    private const bool DefaultCleanStart = true;
    private const bool DefaultCaRequireRevocationCheck = false;

    private static readonly TimeSpan s_defaultKeepAlive = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan s_defaultSessionExpiry = TimeSpan.FromSeconds(3600);
    private static readonly TimeSpan s_defaultConenctionTimeout = TimeSpan.FromSeconds(30);

    public string HostName { get; init; }

    public int TcpPort { get; set; } = DefaultTcpPort;

    public bool UseTls { get; set; } = DefaultUseTls;

    public string? CaFile { get; set; }

    public bool CaRequireRevocationCheck { get; set; } = DefaultCaRequireRevocationCheck;

    public bool CleanStart { get; init; } = DefaultCleanStart;

    public TimeSpan KeepAlive { get; init; } = s_defaultKeepAlive;

    public string? ClientId { get; init; }

    public TimeSpan SessionExpiry { get; set; } = s_defaultSessionExpiry;

    public TimeSpan? ConnectionTimeout { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? PasswordFile { get; set; }

    public string? CertFile { get; set; }

    public string? KeyFile { get; set; }

    public string? KeyFilePassword { get; set; }

    public X509Certificate2? ClientCertificate { get; set; }

    public X509Certificate2Collection? TrustChain { get; set; }

    public string? ModelId { get; init; }

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
            KeyFilePassword = GetStringValue(connectionSettings, nameof(KeyFilePassword));
            Username = GetStringValue(connectionSettings, nameof(Username));
            Password = GetStringValue(connectionSettings, nameof(Password));
            PasswordFile = GetStringValue(connectionSettings, nameof(PasswordFile));
            ModelId = GetStringValue(connectionSettings, nameof(ModelId));
            KeepAlive = GetTimeSpanValue(connectionSettings, nameof(KeepAlive), s_defaultKeepAlive);
            CleanStart = GetBooleanValue(connectionSettings, nameof(CleanStart), DefaultCleanStart);
            SessionExpiry = GetTimeSpanValue(connectionSettings, nameof(SessionExpiry), s_defaultSessionExpiry);
            ConnectionTimeout = GetTimeSpanValue(connectionSettings, nameof(ConnectionTimeout), s_defaultConenctionTimeout);
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
        static string ToUpperCaseFromPascalCase(string pascal) =>
            string.Concat(pascal.Select(x => char.IsUpper(x) ? "_" + x : x.ToString())).ToUpper(CultureInfo.InvariantCulture).TrimStart('_');

        static string Env(string name) =>
                Environment.GetEnvironmentVariable("MQTT_" + ToUpperCaseFromPascalCase(name)) ?? string.Empty;

        string? hostname = Environment.GetEnvironmentVariable("MQTT_HOST_NAME");
        if (string.IsNullOrEmpty(hostname))
        {
            throw AkriMqttException.GetConfigurationInvalidException(
                "MQTT_HOST_NAME",
                string.Empty,
                "Invalid settings in provided Environment Variables: 'MQTT_HOST_NAME' is missing.");
        }

        try
        {
            return new MqttConnectionSettings(hostname)
            {
                ClientId = Env(nameof(ClientId)),
                CertFile = string.IsNullOrEmpty(Env(nameof(CertFile))) ? null : Env(nameof(CertFile)),
                KeyFile = string.IsNullOrEmpty(Env(nameof(KeyFile))) ? null : Env(nameof(KeyFile)),
                Username = string.IsNullOrEmpty(Env(nameof(Username))) ? null : Env(nameof(Username)),
                Password = string.IsNullOrEmpty(Env(nameof(Password))) ? null : Env(nameof(Password)),
                PasswordFile = string.IsNullOrEmpty(Env(nameof(PasswordFile))) ? null : Env(nameof(PasswordFile)),
                KeepAlive = string.IsNullOrEmpty(Env(nameof(KeepAlive))) ? s_defaultKeepAlive : XmlConvertHelper(nameof(KeepAlive), Env(nameof(KeepAlive))),
                SessionExpiry = string.IsNullOrEmpty(Env(nameof(SessionExpiry))) ? s_defaultSessionExpiry : XmlConvertHelper(nameof(SessionExpiry), Env(nameof(SessionExpiry))),
                ConnectionTimeout = string.IsNullOrEmpty(Env(nameof(ConnectionTimeout))) ? null : XmlConvertHelper(nameof(ConnectionTimeout), Env(nameof(ConnectionTimeout))),
                CleanStart = string.IsNullOrEmpty(Env(nameof(CleanStart))) || CheckForValidBooleanInput(nameof(CleanStart), Env(nameof(CleanStart))),
                TcpPort = string.IsNullOrEmpty(Env(nameof(TcpPort))) ? DefaultTcpPort : CheckForValidIntegerInput(nameof(TcpPort), Env(nameof(TcpPort))),
                UseTls = string.IsNullOrEmpty(Env(nameof(UseTls))) || CheckForValidBooleanInput(nameof(UseTls), Env(nameof(UseTls))),
                CaFile = string.IsNullOrEmpty(Env(nameof(CaFile))) ? null : Env(nameof(CaFile)),
                CaRequireRevocationCheck = string.IsNullOrEmpty(Env(nameof(CaRequireRevocationCheck))) || CheckForValidBooleanInput(nameof(CaRequireRevocationCheck), Env(nameof(CaRequireRevocationCheck))),
                KeyFilePassword = Env(nameof(KeyFilePassword)),
                SatAuthFile = Env(nameof(SatAuthFile))
            };
        }
        catch (ArgumentException ex)
        {
            throw AkriMqttException.GetConfigurationInvalidException(ex.ParamName!, Env(ex.ParamName!), "Invalid settings in provided Environment Variables: " + ex.Message, ex);
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

        if (!string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(PasswordFile))
        {
            throw new ArgumentException(
                $"{nameof(Password)} and {nameof(PasswordFile)} file should not be used at the same time.",
                $"{nameof(Password)} and {nameof(PasswordFile)}");
        }

        if (!string.IsNullOrEmpty(SatAuthFile) && (!string.IsNullOrEmpty(Password) || !string.IsNullOrEmpty(PasswordFile)))
        {
            throw new ArgumentException(
                $"{nameof(SatAuthFile)} cannot be used with {nameof(Password)} or {nameof(PasswordFile)}", nameof(SatAuthFile));
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
            if (string.IsNullOrWhiteSpace(KeyFilePassword))
            {
                ClientCertificate = X509Certificate2.CreateFromPemFile(CertFile, KeyFile);
            }
            else
            {
                ClientCertificate = X509Certificate2.CreateFromEncryptedPemFile(CertFile, KeyFilePassword, KeyFile);
            }
        }

        if (!string.IsNullOrWhiteSpace(CaFile))
        {
            X509Certificate2Collection chain = new();
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
                result = XmlConvert.ToTimeSpan(stringValue);
            }
            catch (FormatException ex) // re-throw ex to ArgumentException in order to include the propertyName
            {
                throw new ArgumentException(ex.Message, propertyName, ex);
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
        if (int.TryParse(envVarValue, out int result))
        {
            return result;
        }

        throw new ArgumentException($"{envVarName}={envVarValue}. Expecting an integer value.", envVarName);
    }

    private static bool CheckForValidBooleanInput(string envVarName, string envVarValue)
    {
        if (bool.TryParse(envVarValue, out bool result))
        {
            return result;
        }

        throw new ArgumentException($"{envVarName}={envVarValue}. Expecting a boolean value.", envVarName);
    }

    public override string ToString()
    {
        StringBuilder result = new();
        AppendIfNotNullOrEmpty(result, nameof(HostName), HostName);
        AppendIfNotNullOrEmpty(result, nameof(ClientId), ClientId);
        AppendIfNotNullOrEmpty(result, nameof(ModelId), ModelId);
        AppendIfNotNullOrEmpty(result, nameof(Username), Username);
        AppendIfNotNullOrEmpty(result, nameof(Password), Password);
        AppendIfNotNullOrEmpty(result, nameof(CertFile), CertFile);
        AppendIfNotNullOrEmpty(result, nameof(KeyFile), KeyFile);
        AppendIfNotNullOrEmpty(result, nameof(KeyFilePassword), KeyFilePassword);
        AppendIfNotNullOrEmpty(result, nameof(TcpPort), TcpPort.ToString(CultureInfo.InvariantCulture));
        AppendIfNotNullOrEmpty(result, nameof(CleanStart), CleanStart.ToString());
        AppendIfNotNullOrEmpty(result, nameof(SessionExpiry), XmlConvert.ToString(SessionExpiry));
        AppendIfNotNullOrEmpty(result, nameof(KeepAlive), XmlConvert.ToString(KeepAlive));
        AppendIfNotNullOrEmpty(result, nameof(CaFile), CaFile);
        AppendIfNotNullOrEmpty(result, nameof(UseTls), UseTls.ToString());
        result.Remove(result.Length - 1, 1);
        return result.ToString();
    }

    private static TimeSpan XmlConvertHelper(string paramName, string paramValue)
    {
        try
        {
            return XmlConvert.ToTimeSpan(paramValue);
        }
        catch (FormatException ex) // re-throw ex to ArgumentException in order to include the paramName
        {
            throw new ArgumentException(ex.Message, paramName, ex);
        }
    }
}
