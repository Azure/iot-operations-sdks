// <copyright file="IHistorianQueryExecutor.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Akri.HistorianConnector.Core.Models;

namespace Akri.HistorianConnector.Core.Contracts;

/// <summary>
/// Executes queries against a specific historian system.
/// Implement this interface for each historian type (OSI PI, Wonderware, etc.).
/// </summary>
public interface IHistorianQueryExecutor
{
    /// <summary>
    /// Executes a time-windowed query against the historian.
    /// </summary>
    /// <param name="query">The query definition containing data points to retrieve.</param>
    /// <param name="windowStart">Inclusive start of the time window.</param>
    /// <param name="windowEnd">Exclusive end of the time window.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of samples from the historian.</returns>
    IAsyncEnumerable<HistorianSample> ExecuteAsync(
        HistorianQueryDefinition query,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates that the historian connection and data points are accessible.
    /// Called during connector initialization.
    /// </summary>
    /// <param name="query">The query definition to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with any errors.</returns>
    Task<HistorianValidationResult> ValidateAsync(
        HistorianQueryDefinition query,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of historian query validation.
/// </summary>
public sealed record HistorianValidationResult
{
    /// <summary>
    /// Gets whether the validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets validation errors, if any.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets validation warnings (non-fatal issues).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static HistorianValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static HistorianValidationResult Failure(params string[] errors) =>
        new() { IsValid = false, Errors = errors };
}
