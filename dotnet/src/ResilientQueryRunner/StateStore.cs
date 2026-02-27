// <copyright file="StateStore.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace ResilientQueryRunner;

/// <summary>
/// Represents the state of a query.
/// </summary>
public sealed record QueryState
{
    public required QueryId QueryId { get; init; }
    public required Watermark Watermark { get; init; }
    public DateTimeOffset? LastSuccessUtc { get; init; }
    public DateTimeOffset? LastAttemptUtc { get; init; }
    public int ConsecutiveFailures { get; init; }
    public DateTimeOffset? NextRetryUtc { get; init; }
    public string? LastErrorSummary { get; init; }
}

/// <summary>
/// Stores and retrieves query state.
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// Gets or creates the state for the specified query.
    /// </summary>
    Task<QueryState> GetOrCreateAsync(QueryId queryId, WatermarkKind kind, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Records an attempt for the specified run plan.
    /// </summary>
    Task RecordAttemptAsync(RunPlan plan, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Records a successful run and advances the watermark.
    /// </summary>
    Task RecordSuccessAndAdvanceWatermarkAsync(RunPlan plan, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Records a failed run.
    /// </summary>
    Task RecordFailureAsync(RunPlan plan, string errorSummary, bool retryable, DateTimeOffset nowUtc, DateTimeOffset? nextRetryUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Resets the watermark for the specified query to a new value.
    /// </summary>
    Task ResetWatermarkAsync(QueryId queryId, Watermark newWatermark, CancellationToken cancellationToken);
}

/// <summary>
/// An in-memory implementation of the state store.
/// </summary>
public sealed class InMemoryStateStore : IStateStore
{
    private readonly object _gate = new();
    private readonly Dictionary<QueryId, QueryState> _states = new();

    /// <summary>
    /// Gets or creates the state for the specified query.
    /// </summary>
    public Task<QueryState> GetOrCreateAsync(QueryId queryId, WatermarkKind kind, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_states.TryGetValue(queryId, out var state))
            {
                return Task.FromResult(state);
            }

            var initial = kind switch
            {
                WatermarkKind.Time => Watermark.ForTime(nowUtc),
                WatermarkKind.Index => Watermark.ForIndex(0),
                _ => throw new InvalidOperationException($"Unsupported kind {kind}"),
            };

            state = new QueryState
            {
                QueryId = queryId,
                Watermark = initial,
                LastAttemptUtc = null,
                LastSuccessUtc = null,
                ConsecutiveFailures = 0,
                NextRetryUtc = null,
                LastErrorSummary = null,
            };

            _states.Add(queryId, state);
            return Task.FromResult(state);
        }
    }

    /// <summary>
    /// Records an attempt for the specified run plan.
    /// </summary>
    public Task RecordAttemptAsync(RunPlan plan, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var existing = _states[plan.QueryId];
            _states[plan.QueryId] = existing with { LastAttemptUtc = nowUtc };
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Records a successful run and advances the watermark.
    /// </summary>
    public Task RecordSuccessAndAdvanceWatermarkAsync(RunPlan plan, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var existing = _states[plan.QueryId];
            var newWatermark = plan.AdvanceWatermark
                ? Watermark.Max(existing.Watermark, plan.CandidateWatermarkExclusiveEnd)
                : existing.Watermark;
            _states[plan.QueryId] = existing with
            {
                Watermark = newWatermark,
                LastSuccessUtc = nowUtc,
                ConsecutiveFailures = 0,
                NextRetryUtc = null,
                LastErrorSummary = null,
            };

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Records a failed run.
    /// </summary>
    public Task RecordFailureAsync(RunPlan plan, string errorSummary, bool retryable, DateTimeOffset nowUtc, DateTimeOffset? nextRetryUtc, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var existing = _states[plan.QueryId];
            var next = retryable ? nextRetryUtc : null;
            _states[plan.QueryId] = existing with
            {
                ConsecutiveFailures = existing.ConsecutiveFailures + 1,
                NextRetryUtc = next,
                LastErrorSummary = errorSummary,
            };

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Resets the watermark for the specified query to a new value.
    /// </summary>
    public Task ResetWatermarkAsync(QueryId queryId, Watermark newWatermark, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(queryId, out var existing))
            {
                throw new InvalidOperationException($"Query {queryId} not found.");
            }

            if (existing.Watermark.Kind != newWatermark.Kind)
            {
                throw new InvalidOperationException($"Cannot reset watermark of kind {existing.Watermark.Kind} to {newWatermark.Kind}.");
            }

            _states[queryId] = existing with
            {
                Watermark = newWatermark,
                ConsecutiveFailures = 0,
                NextRetryUtc = null,
                LastErrorSummary = null,
            };

            return Task.CompletedTask;
        }
    }
}
