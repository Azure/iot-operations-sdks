// <copyright file="HistorianCheckpoint.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Serialization;

namespace Akri.HistorianConnector.Core.Models;

/// <summary>
/// Represents the checkpoint state for a historian query, stored in the AIO State Store.
/// </summary>
public sealed record HistorianCheckpoint
{
    /// <summary>
    /// Gets the query ID this checkpoint belongs to.
    /// </summary>
    [JsonPropertyName("queryId")]
    public required string QueryId { get; init; }

    /// <summary>
    /// Gets the watermark kind.
    /// </summary>
    [JsonPropertyName("watermarkKind")]
    public required CheckpointWatermarkKind WatermarkKind { get; init; }

    /// <summary>
    /// Gets the exclusive end watermark timestamp (for time-based watermarks).
    /// </summary>
    [JsonPropertyName("watermarkUtc")]
    public DateTimeOffset? WatermarkUtc { get; init; }

    /// <summary>
    /// Gets the sequence watermark (for sequence-based watermarks).
    /// </summary>
    [JsonPropertyName("watermarkSequence")]
    public long? WatermarkSequence { get; init; }

    /// <summary>
    /// Gets when this checkpoint was last updated.
    /// </summary>
    [JsonPropertyName("updatedUtc")]
    public required DateTimeOffset UpdatedUtc { get; init; }

    /// <summary>
    /// Gets the connector instance that created this checkpoint.
    /// </summary>
    [JsonPropertyName("connectorInstanceId")]
    public required string ConnectorInstanceId { get; init; }

    /// <summary>
    /// Gets the count of successful runs since the connector started.
    /// </summary>
    [JsonPropertyName("successfulRunCount")]
    public long SuccessfulRunCount { get; init; }

    /// <summary>
    /// Gets the count of failed runs since the connector started.
    /// </summary>
    [JsonPropertyName("failedRunCount")]
    public long FailedRunCount { get; init; }

    /// <summary>
    /// Gets optional error information from the last failed run.
    /// </summary>
    [JsonPropertyName("lastError")]
    public string? LastError { get; init; }

    /// <summary>
    /// Gets the timestamp of the last error.
    /// </summary>
    [JsonPropertyName("lastErrorUtc")]
    public DateTimeOffset? LastErrorUtc { get; init; }

    /// <summary>
    /// Watermark kind for checkpoints (avoids conflict with RQR's WatermarkKind).
    /// </summary>
    public enum CheckpointWatermarkKind
    {
        /// <summary>
        /// Time-based watermark.
        /// </summary>
        Time,

        /// <summary>
        /// Index/sequence-based watermark.
        /// </summary>
        Index
    }
}
