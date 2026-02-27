// <copyright file="RqrStateStoreAdapter.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Akri.HistorianConnector.Core.Models;
using Akri.HistorianConnector.Core.StateStore;
using Microsoft.Extensions.Logging;
using ResilientQueryRunner;

namespace Akri.HistorianConnector.Core.Adapters;

/// <summary>
/// Adapts the StateStoreCheckpointRepository to RQR's IStateStore interface.
/// </summary>
internal sealed class RqrStateStoreAdapter : IStateStore
{
    private readonly StateStoreCheckpointRepository _repository;
    private readonly ILogger _logger;
    private readonly Dictionary<QueryId, QueryState> _stateCache = new();

    public RqrStateStoreAdapter(StateStoreCheckpointRepository repository, ILogger logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<QueryState> GetOrCreateAsync(QueryId queryId, ResilientQueryRunner.WatermarkKind kind, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        // Check cache first
        if (_stateCache.TryGetValue(queryId, out var cachedState))
        {
            return cachedState;
        }

        // Try to load from state store
        var checkpoint = await _repository.GetAsync(queryId.Value, cancellationToken).ConfigureAwait(false);

        if (checkpoint != null)
        {
            var watermark = checkpoint.WatermarkKind == Models.HistorianCheckpoint.CheckpointWatermarkKind.Time
                ? Watermark.ForTime(checkpoint.WatermarkUtc ?? nowUtc)
                : Watermark.ForIndex(checkpoint.WatermarkSequence ?? 0);

            var state = new QueryState
            {
                QueryId = queryId,
                Watermark = watermark,
                LastSuccessUtc = checkpoint.UpdatedUtc,
                LastAttemptUtc = checkpoint.UpdatedUtc,
                ConsecutiveFailures = 0,
                NextRetryUtc = null,
                LastErrorSummary = checkpoint.LastError
            };

            _stateCache[queryId] = state;
            return state;
        }

        // Create initial state
        var initialWatermark = kind switch
        {
            ResilientQueryRunner.WatermarkKind.Time => Watermark.ForTime(nowUtc),
            ResilientQueryRunner.WatermarkKind.Index => Watermark.ForIndex(0),
            _ => throw new InvalidOperationException($"Unsupported kind {kind}"),
        };

        var initialState = new QueryState
        {
            QueryId = queryId,
            Watermark = initialWatermark,
            LastAttemptUtc = null,
            LastSuccessUtc = null,
            ConsecutiveFailures = 0,
            NextRetryUtc = null,
            LastErrorSummary = null,
        };

        _stateCache[queryId] = initialState;
        return initialState;
    }

    public Task RecordAttemptAsync(RunPlan plan, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        if (_stateCache.TryGetValue(plan.QueryId, out var state))
        {
            _stateCache[plan.QueryId] = state with { LastAttemptUtc = nowUtc };
        }
        return Task.CompletedTask;
    }

    public async Task RecordSuccessAndAdvanceWatermarkAsync(RunPlan plan, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var newWatermark = plan.Range.CandidateExclusiveEndWatermark();
        
        if (_stateCache.TryGetValue(plan.QueryId, out var state))
        {
            var newState = state with
            {
                Watermark = newWatermark,
                LastSuccessUtc = nowUtc,
                LastAttemptUtc = nowUtc,
                ConsecutiveFailures = 0,
                NextRetryUtc = null,
                LastErrorSummary = null
            };
            _stateCache[plan.QueryId] = newState;

            // Persist to state store
            var watermarkKind = newWatermark.Kind == ResilientQueryRunner.WatermarkKind.Time
                ? HistorianCheckpoint.CheckpointWatermarkKind.Time
                : HistorianCheckpoint.CheckpointWatermarkKind.Index;

            var checkpoint = new HistorianCheckpoint
            {
                QueryId = plan.QueryId.Value,
                WatermarkKind = watermarkKind,
                WatermarkUtc = newWatermark.Kind == ResilientQueryRunner.WatermarkKind.Time ? newWatermark.TimeUtc : null,
                WatermarkSequence = newWatermark.Kind == ResilientQueryRunner.WatermarkKind.Index ? newWatermark.Index : null,
                UpdatedUtc = nowUtc,
                ConnectorInstanceId = "historian-connector",
                SuccessfulRunCount = 1,
                FailedRunCount = 0
            };

            await _repository.SaveAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RecordFailureAsync(RunPlan plan, string errorSummary, bool retryable, DateTimeOffset nowUtc, DateTimeOffset? nextRetryUtc, CancellationToken cancellationToken)
    {
        if (_stateCache.TryGetValue(plan.QueryId, out var state))
        {
            var newState = state with
            {
                LastAttemptUtc = nowUtc,
                ConsecutiveFailures = state.ConsecutiveFailures + 1,
                NextRetryUtc = retryable ? nextRetryUtc : null,
                LastErrorSummary = errorSummary
            };
            _stateCache[plan.QueryId] = newState;

            // Persist failure to state store
            var watermarkKind = state.Watermark.Kind == ResilientQueryRunner.WatermarkKind.Time
                ? HistorianCheckpoint.CheckpointWatermarkKind.Time
                : HistorianCheckpoint.CheckpointWatermarkKind.Index;

            var checkpoint = new HistorianCheckpoint
            {
                QueryId = plan.QueryId.Value,
                WatermarkKind = watermarkKind,
                WatermarkUtc = state.Watermark.Kind == ResilientQueryRunner.WatermarkKind.Time ? state.Watermark.TimeUtc : null,
                WatermarkSequence = state.Watermark.Kind == ResilientQueryRunner.WatermarkKind.Index ? state.Watermark.Index : null,
                UpdatedUtc = nowUtc,
                ConnectorInstanceId = "historian-connector",
                SuccessfulRunCount = 0,
                FailedRunCount = state.ConsecutiveFailures + 1,
                LastError = errorSummary,
                LastErrorUtc = nowUtc
            };

            await _repository.SaveAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ResetWatermarkAsync(QueryId queryId, Watermark newWatermark, CancellationToken cancellationToken)
    {
        if (_stateCache.TryGetValue(queryId, out var state))
        {
            _stateCache[queryId] = state with { Watermark = newWatermark };

            var watermarkKind = newWatermark.Kind == ResilientQueryRunner.WatermarkKind.Time
                ? HistorianCheckpoint.CheckpointWatermarkKind.Time
                : HistorianCheckpoint.CheckpointWatermarkKind.Index;

            var checkpoint = new HistorianCheckpoint
            {
                QueryId = queryId.Value,
                WatermarkKind = watermarkKind,
                WatermarkUtc = newWatermark.Kind == ResilientQueryRunner.WatermarkKind.Time ? newWatermark.TimeUtc : null,
                WatermarkSequence = newWatermark.Kind == ResilientQueryRunner.WatermarkKind.Index ? newWatermark.Index : null,
                UpdatedUtc = DateTimeOffset.UtcNow,
                ConnectorInstanceId = "historian-connector",
                SuccessfulRunCount = 0,
                FailedRunCount = 0
            };

            await _repository.SaveAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        }
    }
}
