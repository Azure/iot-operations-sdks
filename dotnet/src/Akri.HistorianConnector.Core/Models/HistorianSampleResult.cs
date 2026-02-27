// <copyright file="HistorianSampleResult.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace Akri.HistorianConnector.Core.Models;

/// <summary>
/// Represents a single time-series sample from a historian query.
/// </summary>
public sealed record HistorianSample
{
    /// <summary>
    /// Gets the data point/tag identifier.
    /// </summary>
    public required string DataPointName { get; init; }

    /// <summary>
    /// Gets the timestamp of the sample in UTC.
    /// </summary>
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Gets the numeric value of the sample.
    /// </summary>
    public required double Value { get; init; }

    /// <summary>
    /// Gets the optional quality code (historian-specific).
    /// </summary>
    public int? Quality { get; init; }
}

/// <summary>
/// Represents the result of a historian query execution.
/// </summary>
public sealed record HistorianQueryResult
{
    /// <summary>
    /// Gets the query definition that was executed.
    /// </summary>
    public required HistorianQueryDefinition Query { get; init; }

    /// <summary>
    /// Gets the inclusive start time of the queried window.
    /// </summary>
    public required DateTimeOffset WindowStartUtc { get; init; }

    /// <summary>
    /// Gets the exclusive end time of the queried window.
    /// </summary>
    public required DateTimeOffset WindowEndUtc { get; init; }

    /// <summary>
    /// Gets the samples retrieved from the historian.
    /// </summary>
    public required IReadOnlyList<HistorianSample> Samples { get; init; }

    /// <summary>
    /// Gets whether the query completed successfully.
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// Gets an optional error message if the query failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Represents a batch of time-series data ready for publishing.
/// </summary>
public sealed record HistorianBatch
{
    /// <summary>
    /// Gets the query ID.
    /// </summary>
    public required string QueryId { get; init; }

    /// <summary>
    /// Gets the device name.
    /// </summary>
    public required string DeviceName { get; init; }

    /// <summary>
    /// Gets the asset name.
    /// </summary>
    public required string AssetName { get; init; }

    /// <summary>
    /// Gets the dataset name.
    /// </summary>
    public required string DatasetName { get; init; }

    /// <summary>
    /// Gets the inclusive start of the batch window.
    /// </summary>
    public required DateTimeOffset WindowStartUtc { get; init; }

    /// <summary>
    /// Gets the exclusive end of the batch window (becomes the next watermark).
    /// </summary>
    public required DateTimeOffset WindowEndUtc { get; init; }

    /// <summary>
    /// Gets the time series data grouped by data point.
    /// </summary>
    public required IReadOnlyList<TimeSeries> Series { get; init; }

    /// <summary>
    /// Represents a single time series for one data point.
    /// </summary>
    public sealed record TimeSeries
    {
        /// <summary>
        /// Gets the data point/tag identifier.
        /// </summary>
        public required string DataPointName { get; init; }

        /// <summary>
        /// Gets the timestamps in nanoseconds since Unix epoch.
        /// </summary>
        public required IReadOnlyList<long> TimestampsNs { get; init; }

        /// <summary>
        /// Gets the values corresponding to each timestamp.
        /// </summary>
        public required IReadOnlyList<double> Values { get; init; }

        /// <summary>
        /// Gets optional quality codes for each sample.
        /// </summary>
        public IReadOnlyList<int>? Quality { get; init; }
    }
}
