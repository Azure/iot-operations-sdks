// <copyright file="HostContracts.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace ResilientQueryRunner;

/// <summary>
/// Executes queries against the host data source.
/// </summary>
public interface IHostQueryExecutor
{
    /// <summary>
    /// Executes a query for the specified range and returns normalized records.
    /// </summary>
    IAsyncEnumerable<NormalizedRecord> ExecuteAsync(QueryDefinition query, QueryRange range, CancellationToken cancellationToken);
}

/// <summary>
/// Configuration for mapping query results.
/// </summary>
public sealed record MappingConfiguration(object? Value);

/// <summary>
/// Provides mapping configuration for queries.
/// </summary>
public interface IMappingConfigurationProvider
{
    /// <summary>
    /// Gets the mapping configuration for the specified query ID.
    /// </summary>
    MappingConfiguration GetMapping(QueryId queryId);
}

/// <summary>
/// Result of handling a resultset.
/// </summary>
public sealed record AckResult(bool Success, string? Error = null);

/// <summary>
/// Handles processed resultsets.
/// </summary>
public interface IHostResultsetHandler
{
    /// <summary>
    /// Handles the resultset and returns an acknowledgment.
    /// </summary>
    Task<AckResult> HandleAsync(TimeSeriesBatch resultset, MappingConfiguration mappingConfiguration, CancellationToken cancellationToken);
}
