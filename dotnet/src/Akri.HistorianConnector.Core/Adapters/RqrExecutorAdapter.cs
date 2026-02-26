// <copyright file="RqrExecutorAdapter.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Akri.HistorianConnector.Core.Contracts;
using Akri.HistorianConnector.Core.Models;
using Microsoft.Extensions.Logging;
using ResilientQueryRunner;

namespace Akri.HistorianConnector.Core.Adapters;

/// <summary>
/// Adapts IHistorianQueryExecutor to RQR's IHostQueryExecutor interface.
/// </summary>
internal sealed class RqrExecutorAdapter : IHostQueryExecutor
{
    private readonly IHistorianQueryExecutor _executor;
    private readonly ConcurrentDictionary<string, HistorianQueryDefinition> _queryDefinitions;
    private readonly ILogger _logger;

    public RqrExecutorAdapter(
        IHistorianQueryExecutor executor,
        ConcurrentDictionary<string, HistorianQueryDefinition> queryDefinitions,
        ILogger logger)
    {
        _executor = executor;
        _queryDefinitions = queryDefinitions;
        _logger = logger;
    }

    public async IAsyncEnumerable<NormalizedRecord> ExecuteAsync(
        QueryDefinition query,
        QueryRange range,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_queryDefinitions.TryGetValue(query.QueryId.Value, out var historianQuery))
        {
            _logger.LogError("No historian query definition found for {QueryId}", query.QueryId.Value);
            yield break;
        }

        // Extract time window from range
        if (range.Kind != ResilientQueryRunner.WatermarkKind.Time)
        {
            _logger.LogError("Index-based queries not yet supported for historian execution");
            yield break;
        }

        var windowStart = range.FromTimeUtc!.Value;
        var windowEnd = range.ToTimeUtcExclusive!.Value;

        _logger.LogDebug(
            "Executing historian query {QueryId} for window {Start} to {End}",
            query.QueryId.Value,
            windowStart,
            windowEnd);

        await foreach (var sample in _executor.ExecuteAsync(historianQuery, windowStart, windowEnd, cancellationToken))
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace(
                    "Historian sample {QueryId} {SeriesId} {TimestampUtc} {Value} {Quality} {Start} {End}",
                    query.QueryId.Value,
                    sample.DataPointName,
                    sample.TimestampUtc,
                    sample.Value,
                    sample.Quality,
                    windowStart,
                    windowEnd);
            }

            yield return new NormalizedRecord
            {
                SeriesId = sample.DataPointName,
                TimestampUtc = sample.TimestampUtc,
                Value = sample.Value,
                Quality = sample.Quality
            };
        }
    }
}

