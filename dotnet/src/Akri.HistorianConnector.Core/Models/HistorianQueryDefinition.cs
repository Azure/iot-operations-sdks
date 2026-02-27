// <copyright file="HistorianQueryDefinition.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace Akri.HistorianConnector.Core.Models;

/// <summary>
/// Defines a historian query derived from an ADR Asset dataset.
/// This bridges the ADR Asset model to the ResilientQueryRunner's QueryDefinition.
/// </summary>
public sealed record HistorianQueryDefinition
{
    /// <summary>
    /// Gets the unique identifier for this query (typically "{deviceName}/{assetName}/{datasetName}").
    /// </summary>
    public required string QueryId { get; init; }

    /// <summary>
    /// Gets the device name from ADR.
    /// </summary>
    public required string DeviceName { get; init; }

    /// <summary>
    /// Gets the inbound endpoint name from the device.
    /// </summary>
    public required string InboundEndpointName { get; init; }

    /// <summary>
    /// Gets the asset name from ADR.
    /// </summary>
    public required string AssetName { get; init; }

    /// <summary>
    /// Gets the dataset name from the asset.
    /// </summary>
    public required string DatasetName { get; init; }

    /// <summary>
    /// Gets the cron expression for scheduling queries.
    /// Derived from asset/dataset configuration or defaults.
    /// </summary>
    public required string CronExpression { get; init; }

    /// <summary>
    /// Gets the task type for the query (Parse or Copy). Defaults to empty when not specified.
    /// </summary>
    public string TaskType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timezone ID for cron scheduling (default: UTC).
    /// </summary>
    public string TimeZoneId { get; init; } = "UTC";

    /// <summary>
    /// Gets the legacy output topic value. Destination routing is now controlled by
    /// AIO dataset destinations via the SDK forwarding path.
    /// </summary>
    public string OutputTopic { get; init; } = string.Empty;

    /// <summary>
    /// Gets the watermark kind (time-based or sequence-based).
    /// </summary>
    public required WatermarkKind WatermarkKind { get; init; }

    /// <summary>
    /// Gets the window duration for time-based queries.
    /// </summary>
    public TimeSpan? WindowDuration { get; init; }

    /// <summary>
    /// Gets the availability delay (time before data is considered stable in the historian).
    /// </summary>
    public TimeSpan AvailabilityDelay { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the overlap duration for catching late-arriving data.
    /// </summary>
    public TimeSpan Overlap { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the maximum number of windows to process per tick.
    /// </summary>
    public int MaxWindowsPerTick { get; init; } = 100;

    /// <summary>
    /// Gets the historian-specific tag/point references for this dataset.
    /// Derived from Asset.Dataset.DataPoints.
    /// </summary>
    public required IReadOnlyList<HistorianDataPoint> DataPoints { get; init; }

    /// <summary>
    /// Gets optional additional configuration from the asset.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalConfiguration { get; init; }

    /// <summary>
    /// Gets the authentication information resolved from the inbound endpoint.
    /// </summary>
    public ConnectorAuthentication Authentication { get; init; } = ConnectorAuthentication.Anonymous;

    /// <summary>
    /// Gets the SMB server hostname or IP address, parsed from the device inbound endpoint address.
    /// </summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// Gets the SMB server port (default: 445).
    /// </summary>
    public int Port { get; init; } = 445;

    /// <summary>
    /// Gets the SMB share name, parsed from the path segment of the device inbound endpoint address.
    /// </summary>
    public string ShareName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the base path within the share to scan for files.
    /// Resolved from the `basePath` dataset or asset attribute.
    /// </summary>
    public string BasePath { get; init; } = "/";

    /// <summary>
    /// Resolved from the `filePattern` dataset or asset attribute.
    /// </summary>
    public string FilePattern { get; init; } = "*.csv";
}

/// <summary>
/// Represents a single data point (tag) to query from the historian.
/// </summary>
public sealed record HistorianDataPoint
{
    /// <summary>
    /// Gets the name of the data point.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the data source identifier (e.g., PI tag path, Wonderware tag name).
    /// </summary>
    public required string DataSource { get; init; }

    /// <summary>
    /// Gets the historian-specific configuration (e.g., WebId for PI, retrieval mode).
    /// </summary>
    public string? Configuration { get; init; }
}

/// <summary>
/// Specifies the type of watermark tracking for query progress.
/// </summary>
public enum WatermarkKind
{
    /// <summary>
    /// Time-based watermark using exclusive end timestamps.
    /// </summary>
    Time,

    /// <summary>
    /// Sequence-based watermark using monotonic sequence numbers.
    /// </summary>
    Sequence
}
