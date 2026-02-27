// <copyright file="RqrResultHandlerAdapter.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.Collections.Concurrent;
using Akri.HistorianConnector.Core.Contracts;
using Akri.HistorianConnector.Core.Models;
using Microsoft.Extensions.Logging;
using ResilientQueryRunner;

namespace Akri.HistorianConnector.Core.Adapters;

/// <summary>
/// Adapts RQR's IHostResultsetHandler to publish batches via the connector worker.
/// </summary>
internal sealed class RqrResultHandlerAdapter : IHostResultsetHandler
{
    private readonly HistorianConnectorWorker _worker;
    private readonly IHistorianBatchSerializer _serializer;
    private readonly ConcurrentDictionary<string, HistorianQueryDefinition> _queryDefinitions;
    private readonly ILogger _logger;

    public RqrResultHandlerAdapter(
        HistorianConnectorWorker worker,
        IHistorianBatchSerializer serializer,
        ConcurrentDictionary<string, HistorianQueryDefinition> queryDefinitions,
        ILogger logger)
    {
        _worker = worker;
        _serializer = serializer;
        _queryDefinitions = queryDefinitions;
        _logger = logger;
    }

    public async Task<AckResult> HandleAsync(
        TimeSeriesBatch batch,
        MappingConfiguration mappingConfiguration,
        CancellationToken cancellationToken)
    {
        if (!_queryDefinitions.TryGetValue(batch.QueryId.Value, out var query))
        {
            _logger.LogError("No query definition found for {QueryId}", batch.QueryId.Value);
            return new AckResult(false, "Query definition not found");
        }

        try
        {
            // Convert RQR batch to historian batch and serialize payload for SDK forwarding.
            var historianBatch = ConvertBatch(batch, query);
            var serializedPayload = _serializer.Serialize(historianBatch);

            await _worker.PublishBatchAsync(historianBatch, serializedPayload, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "Forwarded batch for query {QueryId}: Series={SeriesCount}",
                batch.QueryId.Value,
                historianBatch.Series.Count);

            return new AckResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish batch for query {QueryId}", batch.QueryId.Value);
            return new AckResult(false, ex.Message);
        }
    }

    private static HistorianBatch ConvertBatch(TimeSeriesBatch rqrBatch, HistorianQueryDefinition query)
    {
        var series = rqrBatch.Series.Select(s => new HistorianBatch.TimeSeries
        {
            DataPointName = s.Id,
            TimestampsNs = s.OffsetNs.Select((offset, i) => s.BaseEpochNs + offset).ToList(),
            Values = s.Values.ToList(),
            Quality = s.Quality?.ToList()
        }).ToList();

        return new HistorianBatch
        {
            QueryId = rqrBatch.QueryId.Value,
            DeviceName = query.DeviceName,
            AssetName = query.AssetName,
            DatasetName = query.DatasetName,
            WindowStartUtc = rqrBatch.ExecutedRange.FromTimeUtc ?? DateTimeOffset.MinValue,
            WindowEndUtc = rqrBatch.ExecutedRange.ToTimeUtcExclusive ?? DateTimeOffset.MaxValue,
            Series = series
        };
    }
}
