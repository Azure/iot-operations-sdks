// <copyright file="RqrEngine.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ResilientQueryRunner;

/// <summary>
/// The resilient query runner engine.
/// </summary>
public sealed class RqrEngine
    : IDisposable
{
    private int _disposed;
    private static readonly Meter _meter = new("ResilientQueryRunner", "1.0.0");
    private static readonly ActivitySource _activitySource = new("ResilientQueryRunner");

    private static readonly Counter<long> _runStarted = _meter.CreateCounter<long>("rqr.run_started_total");
    private static readonly Counter<long> _runSucceeded = _meter.CreateCounter<long>("rqr.run_succeeded_total");
    private static readonly Counter<long> _runFailed = _meter.CreateCounter<long>("rqr.run_failed_total");
    private static readonly Counter<long> _runRetried = _meter.CreateCounter<long>("rqr.run_retried_total");
    private static readonly Counter<long> _queriesRegistered = _meter.CreateCounter<long>("rqr.queries_registered_total");
    private static readonly Counter<long> _queriesUnregistered = _meter.CreateCounter<long>("rqr.queries_unregistered_total");

    private static readonly Histogram<double> _runDurationMs = _meter.CreateHistogram<double>("rqr.run_duration_ms");

    private readonly IStateStore _stateStore;
    private readonly IHostQueryExecutor _executor;
    private readonly IHostResultsetHandler _resultsetHandler;
    private readonly IMappingConfigurationProvider _mappingConfigurationProvider;
    private readonly IClock _clock;
    private readonly IFailureClassifier _failureClassifier;
    private readonly IRetryStrategy _retryStrategy;

    private readonly SemaphoreSlim _globalConcurrency;

    private readonly ConcurrentDictionary<QueryId, QueryDefinition> _queries = new();
    private readonly ConcurrentDictionary<QueryId, ConcurrentQueue<RunPlan>> _readyByQuery = new();
    private readonly ConcurrentDictionary<QueryId, byte> _runningQueries = new();

    private readonly ConcurrentQueue<RunPlan> _adhocQueue = new();
    private readonly ConcurrentQueue<ScheduledRetry> _retryQueue = new();

    public string ConnectorIdentity { get; }

    /// <summary>
    /// Gets the current count of registered queries.
    /// </summary>
    public int RegisteredQueryCount => _queries.Count;

    /// <summary>
    /// Gets all currently registered query IDs.
    /// </summary>
    public IReadOnlyCollection<QueryId> RegisteredQueryIds => _queries.Keys.ToList().AsReadOnly();

    /// <summary>
    /// Initializes a new instance of the engine.
    /// </summary>
    public RqrEngine(
        string connectorIdentity,
        IStateStore stateStore,
        IHostQueryExecutor executor,
        IHostResultsetHandler resultsetHandler,
        IMappingConfigurationProvider mappingConfigurationProvider,
        int globalConcurrencyLimit = 4,
        IClock? clock = null,
        IFailureClassifier? failureClassifier = null,
        IRetryStrategy? retryStrategy = null)
    {
        if (string.IsNullOrWhiteSpace(connectorIdentity))
        {
            throw new ArgumentException("Connector identity is required.", nameof(connectorIdentity));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(globalConcurrencyLimit);

        ConnectorIdentity = connectorIdentity;
        _stateStore = stateStore;
        _executor = executor;
        _resultsetHandler = resultsetHandler;
        _mappingConfigurationProvider = mappingConfigurationProvider;
        _globalConcurrency = new SemaphoreSlim(globalConcurrencyLimit, globalConcurrencyLimit);

        _clock = clock ?? SystemClock.Instance;
        _failureClassifier = failureClassifier ?? new DefaultFailureClassifier();
        _retryStrategy = retryStrategy ?? new ExponentialBackoffRetryStrategy(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5));
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (disposing)
        {
            _globalConcurrency.Dispose();
        }
    }

    /// <summary>
    /// Registers a query for scheduled execution.
    /// If a query with the same ID already exists, it will be replaced.
    /// </summary>
    public void RegisterQuery(QueryDefinition query)
    {
        query.RangeParameters.ValidateFor(query.WatermarkKind);
        _queries[query.QueryId] = query;
        _readyByQuery.TryAdd(query.QueryId, new ConcurrentQueue<RunPlan>());
        _queriesRegistered.Add(1);
    }

    /// <summary>
    /// Unregisters a query, removing it from scheduled execution.
    /// Any pending runs for this query will be discarded.
    /// Running queries will complete but won't be rescheduled.
    /// </summary>
    /// <param name="queryId">The query ID to unregister.</param>
    /// <returns>True if the query was found and removed; false if not found.</returns>
    public bool UnregisterQuery(QueryId queryId)
    {
        var removed = _queries.TryRemove(queryId, out _);

        if (removed)
        {
            // Remove pending runs for this query
            _readyByQuery.TryRemove(queryId, out _);
            _queriesUnregistered.Add(1);
        }

        return removed;
    }

    /// <summary>
    /// Re-registers all queries from the provided list.
    /// Queries not in the new list will be unregistered (gracefully, completing any in-flight runs).
    /// Queries in the new list will be registered or updated.
    /// </summary>
    /// <param name="queries">The new set of query definitions.</param>
    /// <returns>A summary of changes applied.</returns>
    public QueryRegistrationResult ReregisterQueries(IReadOnlyList<QueryDefinition> queries)
    {
        ArgumentNullException.ThrowIfNull(queries);

        var newQueryIds = queries.Select(q => q.QueryId).ToHashSet();
        var currentQueryIds = _queries.Keys.ToHashSet();

        // Find queries to remove (in current but not in new)
        var toRemove = currentQueryIds.Except(newQueryIds).ToList();

        // Find queries to add or update
        var added = 0;
        var updated = 0;
        var removed = 0;

        // Remove queries that are no longer in the configuration
        foreach (var queryId in toRemove)
        {
            if (UnregisterQuery(queryId))
            {
                removed++;
            }
        }

        // Add or update queries
        foreach (var query in queries)
        {
            var isUpdate = _queries.ContainsKey(query.QueryId);
            RegisterQuery(query);

            if (isUpdate)
            {
                updated++;
            }
            else
            {
                added++;
            }
        }

        return new QueryRegistrationResult(added, updated, removed);
    }

    /// <summary>
    /// Requests an ad-hoc execution of the specified query for the given range.
    /// </summary>
    /// <param name="queryId">The ID of the query to run.</param>
    /// <param name="range">The range of data to query.</param>
    /// <param name="advanceWatermark">Whether to advance the watermark after successful execution.</param>
    /// <param name="priorityOverride">Optional priority override for the run; defaults to maximum priority if not specified.</param>
    public void RequestAdhocRun(QueryId queryId, QueryRange range, bool advanceWatermark = false, int? priorityOverride = null)
    {
        var run = new RunPlan
        {
            RunId = RunId.New(),
            QueryId = queryId,
            Range = range,
            CandidateWatermarkExclusiveEnd = range.CandidateExclusiveEndWatermark(),
            EffectivePriority = priorityOverride ?? int.MaxValue,
            IsAdhoc = true,
            AdvanceWatermark = advanceWatermark,
            Attempt = 1,
        };

        _adhocQueue.Enqueue(run);
    }

    /// <summary>
    /// Resets the watermark for the specified query to a new value.
    /// This clears any failure state and allows the query to restart from the new watermark.
    /// </summary>
    public async Task ResetWatermarkAsync(QueryId queryId, Watermark newWatermark, CancellationToken cancellationToken = default)
    {
        if (!_queries.ContainsKey(queryId))
        {
            throw new InvalidOperationException($"Query {queryId} is not registered.");
        }

        await _stateStore.ResetWatermarkAsync(queryId, newWatermark, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes one deterministic scheduling/execution cycle.
    /// Host is expected to call this periodically (e.g., every second).
    /// </summary>
    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var nowUtc = _clock.UtcNow;

        // Release due retries.
        ReleaseDueRetries(nowUtc);

        // Plan scheduled runs.
        foreach (var query in _queries.Values)
        {
            // Skip planning if this query is already executing
            if (_runningQueries.ContainsKey(query.QueryId))
            {
                continue;
            }

            var state = await _stateStore.GetOrCreateAsync(query.QueryId, query.WatermarkKind, nowUtc, cancellationToken).ConfigureAwait(false);

            // Respect retry window.
            if (state.NextRetryUtc is not null && state.NextRetryUtc.Value > nowUtc)
            {
                continue;
            }

            var lastTickUtc = query.Cron.GetLastTickUtc(nowUtc, query.TimeZone);
            var targetToUtc = lastTickUtc - query.RangeParameters.AvailabilityDelay;
            if (targetToUtc < DateTimeOffset.MinValue)
            {
                targetToUtc = DateTimeOffset.MinValue;
            }

            var ranges = RangePlanner.PlanRanges(query.WatermarkKind, state.Watermark, query.RangeParameters, targetToUtc);
            if (ranges.Count == 0)
            {
                continue;
            }

            var queue = _readyByQuery.GetOrAdd(query.QueryId, _ => new ConcurrentQueue<RunPlan>());

            foreach (var range in ranges)
            {
                queue.Enqueue(new RunPlan
                {
                    RunId = RunId.New(),
                    QueryId = query.QueryId,
                    Range = range,
                    CandidateWatermarkExclusiveEnd = range.CandidateExclusiveEndWatermark(),
                    EffectivePriority = query.PriorityParameters.BasePriority,
                    IsAdhoc = false,
                    AdvanceWatermark = true,
                });
            }
        }

        // Merge ad-hoc runs into queues.
        while (_adhocQueue.TryDequeue(out var adhoc))
        {
            _readyByQuery.GetOrAdd(adhoc.QueryId, _ => new ConcurrentQueue<RunPlan>()).Enqueue(adhoc);
        }

        // Execute as many runs as allowed.
        var tasks = new List<Task>();

        foreach (var queryId in _queries.Keys.OrderBy(q => q.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_runningQueries.ContainsKey(queryId))
            {
                continue;
            }

            var queue = _readyByQuery.GetOrAdd(queryId, _ => new ConcurrentQueue<RunPlan>());

            if (!TryDequeueBest(queue, out var plan))
            {
                continue;
            }

            if (!_globalConcurrency.Wait(0, CancellationToken.None))
            {
                // Put it back and stop scheduling further starts this tick.
                queue.Enqueue(plan);
                break;
            }

            if (!_runningQueries.TryAdd(queryId, 0))
            {
                _globalConcurrency.Release();
                queue.Enqueue(plan);
                continue;
            }

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await ExecuteRunAsync(plan, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _runningQueries.TryRemove(queryId, out _);
                    _globalConcurrency.Release();
                }
            }, cancellationToken));
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    private static bool TryDequeueBest(ConcurrentQueue<RunPlan> queue, out RunPlan plan)
    {
        // ConcurrentQueue doesn't support priority. We do a best-effort scan.
        // This is deterministic for unit tests (single-threaded enqueue) and good enough for now.
        plan = default!;

        var list = new List<RunPlan>();
        while (queue.TryDequeue(out var item))
        {
            list.Add(item);
        }

        if (list.Count == 0)
        {
            return false;
        }

        var best = list.OrderByDescending(p => p.EffectivePriority).ThenBy(p => p.RunId.Value).First();
        foreach (var item in list)
        {
            if (item.RunId != best.RunId)
            {
                queue.Enqueue(item);
            }
        }

        plan = best;
        return true;
    }

    private async Task ExecuteRunAsync(RunPlan plan, CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        _runStarted.Add(1);

        using var activity = _activitySource.StartActivity("rqr.run");
        activity?.SetTag("queryId", plan.QueryId.Value);
        activity?.SetTag("runId", plan.RunId.Value);
        activity?.SetTag("range", plan.Range.ToString());
        activity?.SetTag("adhoc", plan.IsAdhoc);
        activity?.SetTag("attempt", plan.Attempt);

        var nowUtc = _clock.UtcNow;
        await _stateStore.RecordAttemptAsync(plan, nowUtc, cancellationToken).ConfigureAwait(false);

        try
        {
            // Check if query is still registered (it may have been removed during execution)
            if (!_queries.TryGetValue(plan.QueryId, out var query))
            {
                // Query was unregistered, skip execution
                return;
            }

            var builder = new NormalizedResultsetBuilder(
                connectorIdentity: ConnectorIdentity,
                queryId: plan.QueryId,
                runId: plan.RunId,
                executedRange: plan.Range,
                candidateWatermarkExclusiveEnd: plan.CandidateWatermarkExclusiveEnd);

            await foreach (var record in _executor.ExecuteAsync(query, plan.Range, cancellationToken).ConfigureAwait(false))
            {
                builder.Add(record);
            }

            var resultset = builder.Build();
            var mapping = _mappingConfigurationProvider.GetMapping(plan.QueryId);
            var ack = await _resultsetHandler.HandleAsync(resultset, mapping, cancellationToken).ConfigureAwait(false);

            if (!ack.Success)
            {
                throw new InvalidOperationException(ack.Error ?? "Host resultset handler failed.");
            }

            await _stateStore.RecordSuccessAndAdvanceWatermarkAsync(plan, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
            _runSucceeded.Add(1);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Check if query is still registered before recording failure
            if (!_queries.TryGetValue(plan.QueryId, out var query))
            {
                // Query was unregistered, don't record failure
                return;
            }

            var retryable = _failureClassifier.IsRetryable(ex);
            var state = await _stateStore.GetOrCreateAsync(plan.QueryId, query.WatermarkKind, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
            DateTimeOffset? nextRetry = null;

            if (retryable)
            {
                nextRetry = _retryStrategy.ComputeNextRetryUtc(_clock.UtcNow, Math.Max(1, state.ConsecutiveFailures + 1));
                EnqueueRetry(plan, nextRetry.Value);
            }

            await _stateStore.RecordFailureAsync(plan, ex.Message, retryable, _clock.UtcNow, nextRetry, cancellationToken).ConfigureAwait(false);
            _runFailed.Add(1);
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            _runDurationMs.Record(elapsedMs);
        }
    }

    private void EnqueueRetry(RunPlan plan, DateTimeOffset dueUtc)
    {
        // Track a delayed retry; it will be re-queued when due.
        _retryQueue.Enqueue(new ScheduledRetry(
            plan with
            {
                Attempt = plan.Attempt + 1,
                // Preserve priority; don't risk overflow (e.g., int.MaxValue).
                EffectivePriority = plan.EffectivePriority,
            },
            dueUtc));

        _runRetried.Add(1);
    }

    private void ReleaseDueRetries(DateTimeOffset nowUtc)
    {
        // Best-effort: pull all items, re-enqueue those not due.
        var pending = new List<ScheduledRetry>();
        while (_retryQueue.TryDequeue(out var item))
        {
            if (item.DueUtc <= nowUtc)
            {
                _readyByQuery.GetOrAdd(item.Plan.QueryId, _ => new ConcurrentQueue<RunPlan>()).Enqueue(item.Plan);
            }
            else
            {
                pending.Add(item);
            }
        }

        foreach (var p in pending)
        {
            _retryQueue.Enqueue(p);
        }
    }

    private readonly record struct ScheduledRetry(RunPlan Plan, DateTimeOffset DueUtc);
}

/// <summary>
/// Result of a query re-registration operation.
/// </summary>
/// <param name="Added">Number of new queries added.</param>
/// <param name="Updated">Number of existing queries updated.</param>
/// <param name="Removed">Number of queries removed.</param>
public readonly record struct QueryRegistrationResult(int Added, int Updated, int Removed)
{
    /// <summary>
    /// Gets the total number of changes made.
    /// </summary>
    public int TotalChanges => Added + Updated + Removed;

    /// <summary>
    /// Gets whether any changes were made.
    /// </summary>
    public bool HasChanges => TotalChanges > 0;

    public override string ToString() => $"Added: {Added}, Updated: {Updated}, Removed: {Removed}";
}
