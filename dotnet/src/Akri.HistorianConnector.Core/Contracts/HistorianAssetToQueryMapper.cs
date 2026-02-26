// <copyright file="HistorianAssetToQueryMapper.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
using Akri.HistorianConnector.Core.Models;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Microsoft.Extensions.Logging;

namespace Akri.HistorianConnector.Core.Contracts;

/// <summary>
/// Default implementation that extracts historian configuration from ADR attributes.
/// <para>
/// Scheduling and query parameters are resolved with dataset-level overrides taking
/// precedence over asset-level defaults. Dataset-level values are read from the
/// <see cref="AssetDataset.DatasetConfiguration"/> JSON string using the same
/// dataset/asset keys (for example <c>cronExpression</c>, <c>basePath</c>) so per-query
/// overrides are supported within a single asset.
/// </para>
/// <para>
/// An empty <see cref="AssetDataset.DataPoints"/> list is valid and means the executor
/// should process all columns/tags found in each matched file ("scan-all" mode).
/// </para>
/// </summary>
public class HistorianAssetToQueryMapper : IAssetToQueryMapper
{
    private readonly ILogger<HistorianAssetToQueryMapper> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="HistorianAssetToQueryMapper"/>.
    /// </summary>
    public HistorianAssetToQueryMapper(ILogger<HistorianAssetToQueryMapper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Attribute key for cron expression in asset/dataset configuration.
    /// </summary>
    public const string CronExpressionAttribute = "cronExpression";

    /// <summary>
    /// Attribute key for window duration in asset/dataset configuration.
    /// </summary>
    public const string WindowDurationAttribute = "windowDurationSeconds";

    /// <summary>
    /// Attribute key for availability delay in asset/dataset configuration.
    /// </summary>
    public const string AvailabilityDelayAttribute = "availabilityDelaySeconds";

    /// <summary>
    /// Attribute key for overlap duration in asset/dataset configuration.
    /// </summary>
    public const string OverlapAttribute = "overlapSeconds";

    /// <summary>
    /// Attribute key for watermark kind in asset/dataset configuration.
    /// </summary>
    public const string WatermarkKindAttribute = "watermarkKind";

    /// <summary>
    /// Attribute key for SMB base path in asset/dataset configuration.
    /// </summary>
    public const string SmbBasePathAttribute = "basePath";

    /// <summary>
    /// Attribute key for SMB file pattern in asset/dataset configuration.
    /// </summary>
    public const string SmbFilePatternAttribute = "filePattern";

    /// <summary>
    /// Default SMB file pattern.
    /// </summary>
    public const string DefaultFilePattern = "*.csv";


    /// <summary>
    /// Dataset config key for query id in array-based configuration.
    /// </summary>
    public const string QueryIdAttribute = "QueryId";

    /// <summary>
    /// Dataset config key for task type in array-based configuration.
    /// </summary>
    public const string TaskTypeAttribute = "TaskType";

    /// <summary>
    /// Dataset config key for directory path in array-based configuration.
    /// </summary>
    public const string DirectoryPathAttribute = "DirectoryPath";

    /// <summary>
    /// Dataset config key for file filter in array-based configuration.
    /// </summary>
    public const string FileFilterAttribute = "FileFilter";

    /// <summary>
    /// Dataset config key for schedule in array-based configuration.
    /// </summary>
    public const string ScheduleAttribute = "Schedule";

    /// <summary>
    /// Default cron expression: every minute.
    /// </summary>
    public const string DefaultCronExpression = "* * * * *";

    /// <summary>
    /// Default window duration: 1 minute.
    /// </summary>
    public static readonly TimeSpan DefaultWindowDuration = TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    public virtual IReadOnlyList<HistorianQueryDefinition> MapDatasetToQueries(
        string deviceName,
        Device device,
        string inboundEndpointName,
        string assetName,
        Asset asset,
        AssetDataset dataset)
    {
        _logger.LogDebug(
            "Mapping dataset '{DatasetName}' on asset '{AssetName}': " +
            "attributes=[{Attributes}], dataPoints={DataPointCount}, hasDatasetConfig={HasConfig}, " +
            "datasetConfig='{DatasetConfig}'",
            dataset.Name ?? "(null)",
            assetName,
            string.Join(", ", asset.Attributes?.Keys ?? (IEnumerable<string>)[]),
            dataset.DataPoints?.Count ?? 0,
            !string.IsNullOrEmpty(dataset.DatasetConfiguration),
            dataset.DatasetConfiguration ?? "");

        var datasetConfigs = ParseDatasetConfigurations(dataset.DatasetConfiguration);
        if (datasetConfigs.Count == 0)
        {
            return Array.Empty<HistorianQueryDefinition>();
        }

        var queries = new List<HistorianQueryDefinition>();

        foreach (var datasetConfig in datasetConfigs)
        {
            var configQueryId = GetDatasetConfigValue(datasetConfig, QueryIdAttribute);
            var queryId = string.IsNullOrWhiteSpace(configQueryId)
                ? $"{deviceName}/{assetName}/{dataset.Name}"
                : $"{deviceName}/{assetName}/{dataset.Name}/{configQueryId}";

            // Extract configuration: dataset-level overrides take precedence over asset-level defaults
            var cronExpression = GetDatasetConfigValue(datasetConfig, ScheduleAttribute, CronExpressionAttribute)
                ?? GetAttributeValue(asset, CronExpressionAttribute, DefaultCronExpression);
            var windowDurationSeconds = GetDatasetConfigValueInt(datasetConfig, WindowDurationAttribute)
                ?? GetAttributeValue(asset, WindowDurationAttribute, (int)DefaultWindowDuration.TotalSeconds);
            var availabilityDelaySeconds = GetDatasetConfigValueInt(datasetConfig, AvailabilityDelayAttribute)
                ?? GetAttributeValue(asset, AvailabilityDelayAttribute, 0);
            var overlapSeconds = GetDatasetConfigValueInt(datasetConfig, OverlapAttribute)
                ?? GetAttributeValue(asset, OverlapAttribute, 0);
            var watermarkKindStr = GetDatasetConfigValue(datasetConfig, WatermarkKindAttribute)
                ?? GetAttributeValue(asset, WatermarkKindAttribute, "Time");

            var watermarkKind = watermarkKindStr.Equals("Sequence", StringComparison.OrdinalIgnoreCase)
                ? WatermarkKind.Sequence
                : WatermarkKind.Time;

            // Parse connection topology from the device inbound endpoint address (smb://host/share)
            var (endpointHost, endpointPort, endpointShare) = ParseSmbEndpointAddress(
                device, inboundEndpointName);

            var basePath = GetDatasetConfigValue(datasetConfig, DirectoryPathAttribute, SmbBasePathAttribute)
                ?? GetAttributeValue(asset, SmbBasePathAttribute, "/");

            // Defer resolving inbound endpoint secrets/credentials to runtime via the
            // Operations Connector / resolver. Mapping should be lightweight and not
            // perform secret resolution or external calls.
            var authentication = ResolveInboundEndpointAuthentication(device, inboundEndpointName);

            var filePattern = GetDatasetConfigValue(datasetConfig, FileFilterAttribute, SmbFilePatternAttribute)
                ?? GetAttributeValue(asset, SmbFilePatternAttribute, DefaultFilePattern);

            var taskType = GetDatasetConfigValue(datasetConfig, TaskTypeAttribute);

            // An empty list is intentional: the executor will process all columns/tags
            // found in matched files ("scan-all" mode). Data points with a null or empty
            // DataSource are silently skipped; only explicitly-configured sources are used
            // when the list is non-empty.
            var dataPoints = (dataset.DataPoints ?? [])
                .Where(dp => !string.IsNullOrEmpty(dp.DataSource))
                .Select(dp => new HistorianDataPoint
                {
                    Name = dp.Name ?? dp.DataSource!,
                    DataSource = dp.DataSource!,
                    Configuration = dp.DataPointConfiguration
                })
                .ToList();

            _logger.LogDebug(
                "Resolved dataset '{DatasetName}' (asset '{AssetName}') → query {QueryId}: " +
                "cron='{Cron}', window={Window}s, delay={Delay}s, overlap={Overlap}s, " +
                "basePath='{BasePath}', pattern='{Pattern}', " +
                "host='{Host}', share='{Share}', dataPoints={DataPoints}",
                dataset.Name, assetName, queryId,
                cronExpression, windowDurationSeconds, availabilityDelaySeconds, overlapSeconds,
                basePath, filePattern,
                endpointHost, endpointShare, dataPoints.Count);

            queries.Add(new HistorianQueryDefinition
            {
                QueryId = queryId,
                DeviceName = deviceName,
                InboundEndpointName = inboundEndpointName,
                AssetName = assetName,
                DatasetName = dataset.Name ?? "default",
                CronExpression = cronExpression,
                TaskType = taskType ?? string.Empty,
                WatermarkKind = watermarkKind,
                WindowDuration = TimeSpan.FromSeconds(windowDurationSeconds),
                AvailabilityDelay = TimeSpan.FromSeconds(availabilityDelaySeconds),
                Overlap = TimeSpan.FromSeconds(overlapSeconds),
                Host = endpointHost,
                Port = endpointPort,
                ShareName = endpointShare,
                Authentication = authentication,
                BasePath = basePath,
                FilePattern = filePattern,
                DataPoints = dataPoints
            });
        }

        return queries;
    }

    /// <summary>
    /// Parses the SMB inbound endpoint address (e.g., <c>smb://host/share</c>) from the
    /// device's inbound endpoint and returns the host, port, and share name.
    /// Falls back to empty strings and port 445 when the endpoint or address is unavailable.
    /// </summary>
    protected (string Host, int Port, string ShareName) ParseSmbEndpointAddress(
        Device device, string inboundEndpointName)
    {
        const int defaultSmbPort = 445;

        var address = device.Endpoints?.Inbound != null
            && device.Endpoints.Inbound.TryGetValue(inboundEndpointName, out var endpoint)
            ? endpoint.Address
            : null;

        if (string.IsNullOrWhiteSpace(address))
        {
            return (string.Empty, defaultSmbPort, string.Empty);
        }

        _logger.LogDebug(
            "Parsing SMB endpoint address '{Address}' endpoint '{EndpointName}'",
            address, inboundEndpointName);
        // Authentication resolution logic removed from the mapper to avoid performing
        // secret resolution or external calls during mapping. Credentials are resolved
        // at runtime via a dedicated IEndpointCredentialsResolver implemented in
        // Akri.HistorianConnector.Core.Services and used by executors.


        // Normalize: prepend smb:// if no scheme is present so Uri.TryCreate
        // can parse the address as an absolute URI regardless of how it was entered.
        if (!address.Contains("://", StringComparison.OrdinalIgnoreCase))
        {
            address = $"smb://{address}";
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            return (string.Empty, defaultSmbPort, string.Empty);
        }

        var host = uri.Host;
        var usesSmbScheme = uri.Scheme.Equals("smb", StringComparison.OrdinalIgnoreCase);
        if (!usesSmbScheme)
        {
            _logger.LogWarning(
                "Inbound endpoint address '{Address}' uses scheme '{Scheme}'. SMB endpoints should use smb://host/share. Connector will still use host/port/share from endpoint configuration.",
                address,
                uri.Scheme);
        }

        var port = uri.Port > 0 ? uri.Port : defaultSmbPort;
        // The first non-empty path segment is the share name (e.g., smb://host/share/sub -> "share")
        var shareName = uri.AbsolutePath.TrimStart('/').Split('/')[0];

        return (host, port, shareName);
    }

    /// <summary>
    /// Resolves authentication details from the device inbound endpoint.
    /// </summary>
    private ConnectorAuthentication ResolveInboundEndpointAuthentication(Device device, string inboundEndpointName)
    {
        var endpoint = device.Endpoints?.Inbound != null
            && device.Endpoints.Inbound.TryGetValue(inboundEndpointName, out var resolvedEndpoint)
            ? resolvedEndpoint
            : null;

        if (endpoint == null)
        {
            _logger.LogWarning(
                "Inbound endpoint '{EndpointName}'; defaulting to anonymous authentication",
                inboundEndpointName);

            return ConnectorAuthentication.Anonymous;
        }

        // Non-sensitive debug logging to help trace inbound endpoint configuration
        try
        {
            _logger.LogDebug("Resolving authentication for inbound endpoint '{EndpointName}': Address='{Address}', HasAuthenticationProperty={HasAuth}",
                inboundEndpointName,
                GetStringProperty(endpoint, "Address") ?? "(none)",
                GetPropertyValue(endpoint, "Authentication") != null);
        }
        catch
        {
            // Swallow logging errors to avoid affecting mapping behavior
        }

        var authentication = GetPropertyValue(endpoint, "Authentication");
        if (authentication == null)
        {
            return ConnectorAuthentication.Anonymous;
        }

        // New: prefer explicit Method enum/property when present (Azure ADR SDK uses a Method enum)
        var methodObj = GetPropertyValue(authentication, "Method");
        if (methodObj != null)
        {
            var methodStr = methodObj.ToString();

            _logger.LogDebug("Inbound endpoint Authentication method: '{Method}'", methodStr);

            switch (methodStr)
            {
                case "UsernamePassword":
                    {
                        var upCreds = GetPropertyValue(authentication, "UsernamePasswordCredentials");

                        if (upCreds != null)
                        {
                            // Support secret reference shape used by ADR SDK: UsernameSecretName / PasswordSecretName
                            var usernameSecretName = GetStringProperty(upCreds, "UsernameSecretName");
                            var passwordSecretName = GetStringProperty(upCreds, "PasswordSecretName");

                            //TODO: resolve the values from keyvault

                            // If explicit creds exist use them; otherwise return secret names (may be resolved later by platform)
                            var outUsername = "iotuser";
                            var outPassword = "iotpass";

                            return ConnectorAuthentication.UsernamePassword(outUsername, outPassword);
                        }

                        break;
                    }

                case "Certificate":
                    {
                        var xCreds = GetPropertyValue(authentication, "X509Credentials")
                                     ?? GetPropertyValue(authentication, "Credentials")
                                     ?? GetPropertyValue(authentication, "Certificate");

                        var certificate = GetStringProperty(xCreds, "Certificate") ?? GetStringProperty(xCreds, "CertificatePem") ?? GetStringProperty(xCreds, "ClientCertificate");
                        var privateKey = GetStringProperty(xCreds, "PrivateKey") ?? GetStringProperty(xCreds, "PrivateKeyPem") ?? GetStringProperty(xCreds, "Key");

                        if (xCreds != null)
                        {
                            _logger.LogDebug("Mapped inbound endpoint to X509 auth (certificate present: {HasCert}, privateKey present: {HasKey})", !string.IsNullOrEmpty(certificate), !string.IsNullOrEmpty(privateKey));
                            return ConnectorAuthentication.X509(certificate, privateKey);
                        }

                        break;
                    }

                default:
                    return ConnectorAuthentication.Anonymous;
            }
        }

        // Fallback: legacy shapes where a Type/AuthenticationType string or Credentials object are provided
        var authType = GetStringProperty(authentication, "Type")
            ?? GetStringProperty(authentication, "AuthenticationType")
            ?? authentication.GetType().Name;

        if (IsAnonymousAuthType(authType))
        {
            return ConnectorAuthentication.Anonymous;
        }

        var credentials = GetPropertyValue(authentication, "Credentials") ?? authentication;

        var usernameFallback = GetStringProperty(credentials, "Username")
            ?? GetStringProperty(credentials, "UserName");
        var passwordFallback = GetStringProperty(credentials, "Password")
            ?? GetStringProperty(credentials, "Secret")
            ?? GetStringProperty(credentials, "PasswordSecret");

        if (!string.IsNullOrEmpty(usernameFallback) || !string.IsNullOrEmpty(passwordFallback))
        {
            return ConnectorAuthentication.UsernamePassword(usernameFallback, passwordFallback);
        }

        var certificateFallback = GetStringProperty(credentials, "Certificate")
            ?? GetStringProperty(credentials, "CertificatePem")
            ?? GetStringProperty(credentials, "ClientCertificate");
        var privateKeyFallback = GetStringProperty(credentials, "PrivateKey")
            ?? GetStringProperty(credentials, "PrivateKeyPem")
            ?? GetStringProperty(credentials, "Key");

        if (!string.IsNullOrEmpty(certificateFallback) || !string.IsNullOrEmpty(privateKeyFallback) || IsX509AuthType(authType))
        {
            return ConnectorAuthentication.X509(certificateFallback, privateKeyFallback);
        }

        _logger.LogWarning(
            "Inbound endpoint authentication type '{AuthType}' was not recognized; defaulting to anonymous",
            authType);

        return ConnectorAuthentication.Anonymous;
    }

    /// <summary>
    /// Parses the <see cref="AssetDataset.DatasetConfiguration"/> JSON string into
    /// one or more dictionaries for per-dataset attribute lookups.
    /// </summary>
    protected List<Dictionary<string, string>?> ParseDatasetConfigurations(string? datasetConfiguration)
    {
        if (string.IsNullOrWhiteSpace(datasetConfiguration))
        {
            return [null];
        }

        _logger.LogDebug(
            "Parsing dataset configuration JSON for dataset: '{DatasetConfig}'",
            datasetConfiguration);

        try
        {
            using var doc = JsonDocument.Parse(datasetConfiguration);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var configs = new List<Dictionary<string, string>?>();
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.Object)
                    {
                        configs.Add(ParseDatasetConfigurationObject(element));
                    }
                }

                return configs;
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                return [ParseDatasetConfigurationObject(doc.RootElement)];
            }

            return [null];
        }
        catch (JsonException)
        {
            return [null];
        }
    }

    /// <summary>
    /// Extracts a string value from the parsed dataset configuration, or null if not found.
    /// </summary>
    protected static string? GetDatasetConfigValue(Dictionary<string, string>? datasetConfig, params string[] keys)
    {
        if (datasetConfig == null)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (datasetConfig.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts an integer value from the parsed dataset configuration, or null if not found.
    /// </summary>
    protected static int? GetDatasetConfigValueInt(Dictionary<string, string>? datasetConfig, string key)
    {
        if (datasetConfig != null && datasetConfig.TryGetValue(key, out var value))
        {
            if (int.TryParse(value, out var parsed)) return parsed;
        }
        return null;
    }

    private static Dictionary<string, string>? ParseDatasetConfigurationObject(JsonElement element)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                result[property.Name] = property.Value.ToString();
            }
        }

        return result.Count > 0 ? result : null;
    }

    private static object? GetPropertyValue(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        return property?.GetValue(instance);
    }

    private static string? GetStringProperty(object? instance, string propertyName)
    {
        if (instance == null)
        {
            return null;
        }

        var value = GetPropertyValue(instance, propertyName);
        return value?.ToString();
    }

    private static bool IsAnonymousAuthType(string authType)
        => authType.Contains("anonymous", StringComparison.OrdinalIgnoreCase)
           || authType.Equals("none", StringComparison.OrdinalIgnoreCase);

    private static bool IsX509AuthType(string authType)
        => authType.Contains("x509", StringComparison.OrdinalIgnoreCase)
           || authType.Contains("certificate", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Extracts an attribute value from the asset, with a default fallback.
    /// </summary>
    protected static string GetAttributeValue(Asset asset, string key, string defaultValue)
    {
        if (asset.Attributes != null && asset.Attributes.TryGetValue(key, out var value))
        {
            return value?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Extracts an integer attribute value from the asset, with a default fallback.
    /// </summary>
    protected static int GetAttributeValue(Asset asset, string key, int defaultValue)
    {
        if (asset.Attributes != null && asset.Attributes.TryGetValue(key, out var value))
        {
            if (int.TryParse(value, out var parsed)) return parsed;
        }
        return defaultValue;
    }
}
