// <copyright file="WatermarkData.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace Akri.Connector.SMB.Models;

/// <summary>
/// Watermark data for tracking file processing progress.
/// </summary>
public sealed class WatermarkData
{
    /// <summary>
    /// Gets or sets the query ID this watermark belongs to.
    /// </summary>
    public required string QueryId { get; set; }

    /// <summary>
    /// Gets or sets the watermark timestamp (last processed file modification time).
    /// </summary>
    public DateTimeOffset Watermark { get; set; }

    /// <summary>
    /// Gets or sets when this watermark was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
