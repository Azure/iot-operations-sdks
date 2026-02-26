// <copyright file="HistorianConnectorOptions.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Akri.HistorianConnector.Core;

/// <summary>
/// Configuration options for the historian connector.
/// </summary>
public sealed class HistorianConnectorOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "HistorianConnector";

    /// <summary>
    /// Gets or sets the unique identifier for this connector instance.
    /// </summary>
    [Required]
    public string ConnectorInstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the interval between RQR engine ticks in seconds.
    /// </summary>
    [Range(1, 3600)]
    public int TickIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the global concurrency limit for parallel query execution.
    /// </summary>
    [Range(1, 100)]
    public int GlobalConcurrencyLimit { get; set; } = 4;

    /// <summary>
    /// Gets or sets the graceful shutdown timeout in seconds.
    /// </summary>
    [Range(1, 300)]
    public int GracefulShutdownTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether to validate historian connectivity on startup.
    /// </summary>
    public bool ValidateOnStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets the default window duration in seconds for time-based queries.
    /// </summary>
    [Range(1, 86400)]
    public int DefaultWindowDurationSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the default availability delay in seconds.
    /// </summary>
    [Range(0, 3600)]
    public int DefaultAvailabilityDelaySeconds { get; set; } = 0;
}
