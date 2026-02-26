// <copyright file="RqrEngineReregistrationTests.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using FluentAssertions;
using Xunit;

namespace ResilientQueryRunner.Tests;

/// <summary>
/// Tests for RqrEngine query registration and reregistration functionality.
/// </summary>
public sealed class RqrEngineReregistrationTests
{
    private readonly FakeStateStore _stateStore = new();
    private readonly FakeExecutor _executor = new();
    private readonly FakeResultHandler _resultHandler = new();
    private readonly FakeMappingProvider _mappingProvider = new();
    private readonly FakeClock _clock = new(DateTimeOffset.UtcNow);

    [Fact]
    public void UnregisterQuery_RemovesQueryFromEngine()
    {
        // Arrange
        var engine = CreateEngine();
        var queryId = new QueryId("test-query");
        var query = CreateQuery(queryId);
        engine.RegisterQuery(query);

        engine.RegisteredQueryCount.Should().Be(1);
        engine.RegisteredQueryIds.Should().Contain(queryId);

        // Act
        var result = engine.UnregisterQuery(queryId);

        // Assert
        result.Should().BeTrue();
        engine.RegisteredQueryCount.Should().Be(0);
        engine.RegisteredQueryIds.Should().NotContain(queryId);
    }

    [Fact]
    public void UnregisterQuery_ReturnsFalse_WhenQueryNotFound()
    {
        // Arrange
        var engine = CreateEngine();
        var queryId = new QueryId("non-existent");

        // Act
        var result = engine.UnregisterQuery(queryId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ReregisterQueries_AddsNewQueries()
    {
        // Arrange
        var engine = CreateEngine();
        var query1 = CreateQuery(new QueryId("query-1"));
        var query2 = CreateQuery(new QueryId("query-2"));

        // Act
        var result = engine.ReregisterQueries([query1, query2]);

        // Assert
        result.Added.Should().Be(2);
        result.Updated.Should().Be(0);
        result.Removed.Should().Be(0);
        result.HasChanges.Should().BeTrue();
        engine.RegisteredQueryCount.Should().Be(2);
    }

    [Fact]
    public void ReregisterQueries_UpdatesExistingQueries()
    {
        // Arrange
        var engine = CreateEngine();
        var queryId = new QueryId("query-1");
        var originalQuery = CreateQuery(queryId, cronExpression: "0 * * * * *");
        engine.RegisterQuery(originalQuery);

        var updatedQuery = CreateQuery(queryId, cronExpression: "0 */5 * * * *");

        // Act
        var result = engine.ReregisterQueries([updatedQuery]);

        // Assert
        result.Added.Should().Be(0);
        result.Updated.Should().Be(1);
        result.Removed.Should().Be(0);
        engine.RegisteredQueryCount.Should().Be(1);
    }

    [Fact]
    public void ReregisterQueries_RemovesOldQueries()
    {
        // Arrange
        var engine = CreateEngine();
        var query1 = CreateQuery(new QueryId("query-1"));
        var query2 = CreateQuery(new QueryId("query-2"));
        var query3 = CreateQuery(new QueryId("query-3"));
        engine.RegisterQuery(query1);
        engine.RegisterQuery(query2);
        engine.RegisterQuery(query3);

        engine.RegisteredQueryCount.Should().Be(3);

        // Act - Keep only query-1
        var result = engine.ReregisterQueries([query1]);

        // Assert
        result.Added.Should().Be(0);
        result.Updated.Should().Be(1); // query-1 updated
        result.Removed.Should().Be(2); // query-2 and query-3 removed
        engine.RegisteredQueryCount.Should().Be(1);
        engine.RegisteredQueryIds.Should().Contain(new QueryId("query-1"));
        engine.RegisteredQueryIds.Should().NotContain(new QueryId("query-2"));
        engine.RegisteredQueryIds.Should().NotContain(new QueryId("query-3"));
    }

    [Fact]
    public void ReregisterQueries_HandlesComplexScenario()
    {
        // Arrange
        var engine = CreateEngine();
        var queryA = CreateQuery(new QueryId("query-a"));
        var queryB = CreateQuery(new QueryId("query-b"));
        engine.RegisterQuery(queryA);
        engine.RegisterQuery(queryB);

        // New configuration: keep query-a, remove query-b, add query-c
        var queryAUpdated = CreateQuery(new QueryId("query-a"), cronExpression: "0 */10 * * * *");
        var queryC = CreateQuery(new QueryId("query-c"));

        // Act
        var result = engine.ReregisterQueries([queryAUpdated, queryC]);

        // Assert
        result.Added.Should().Be(1); // query-c
        result.Updated.Should().Be(1); // query-a
        result.Removed.Should().Be(1); // query-b
        result.TotalChanges.Should().Be(3);
        engine.RegisteredQueryCount.Should().Be(2);
        engine.RegisteredQueryIds.Should().Contain(new QueryId("query-a"));
        engine.RegisteredQueryIds.Should().Contain(new QueryId("query-c"));
        engine.RegisteredQueryIds.Should().NotContain(new QueryId("query-b"));
    }

    [Fact]
    public void ReregisterQueries_WithEmptyList_RemovesAllQueries()
    {
        // Arrange
        var engine = CreateEngine();
        engine.RegisterQuery(CreateQuery(new QueryId("query-1")));
        engine.RegisterQuery(CreateQuery(new QueryId("query-2")));

        engine.RegisteredQueryCount.Should().Be(2);

        // Act
        var result = engine.ReregisterQueries([]);

        // Assert
        result.Added.Should().Be(0);
        result.Updated.Should().Be(0);
        result.Removed.Should().Be(2);
        engine.RegisteredQueryCount.Should().Be(0);
    }

    [Fact]
    public void RegisteredQueryCount_ReturnsCorrectCount()
    {
        // Arrange
        var engine = CreateEngine();

        engine.RegisteredQueryCount.Should().Be(0);

        engine.RegisterQuery(CreateQuery(new QueryId("query-1")));
        engine.RegisteredQueryCount.Should().Be(1);

        engine.RegisterQuery(CreateQuery(new QueryId("query-2")));
        engine.RegisteredQueryCount.Should().Be(2);

        engine.UnregisterQuery(new QueryId("query-1"));
        engine.RegisteredQueryCount.Should().Be(1);
    }

    [Fact]
    public void RegisteredQueryIds_ReturnsAllQueryIds()
    {
        // Arrange
        var engine = CreateEngine();
        var id1 = new QueryId("query-1");
        var id2 = new QueryId("query-2");
        var id3 = new QueryId("query-3");

        engine.RegisterQuery(CreateQuery(id1));
        engine.RegisterQuery(CreateQuery(id2));
        engine.RegisterQuery(CreateQuery(id3));

        // Act
        var ids = engine.RegisteredQueryIds;

        // Assert
        ids.Should().HaveCount(3);
        ids.Should().Contain(id1);
        ids.Should().Contain(id2);
        ids.Should().Contain(id3);
    }

    [Fact]
    public async Task TickAsync_SkipsUnregisteredQuery()
    {
        // Arrange
        var engine = CreateEngine();
        var queryId = new QueryId("test-query");
        var query = CreateQuery(queryId);
        engine.RegisterQuery(query);

        // Initialize state
        await _stateStore.GetOrCreateAsync(queryId, WatermarkKind.Time, _clock.UtcNow, CancellationToken.None);

        // Unregister before tick
        engine.UnregisterQuery(queryId);

        // Act
        await engine.TickAsync(CancellationToken.None);

        // Assert - No execution should have occurred
        _executor.ExecutionCount.Should().Be(0);
    }

    private RqrEngine CreateEngine()
    {
        return new RqrEngine(
            "test-connector",
            _stateStore,
            _executor,
            _resultHandler,
            _mappingProvider,
            globalConcurrencyLimit: 4,
            clock: _clock);
    }

    private static QueryDefinition CreateQuery(QueryId queryId, string cronExpression = "0 * * * * *")
    {
        return new QueryDefinition
        {
            QueryId = queryId,
            Cron = CronExpression.Parse(cronExpression),
            TimeZone = TimeZoneInfo.Utc,
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = TimeSpan.FromMinutes(1),
                CatchUpMaxWindowsPerTick = 10,
            },
        };
    }

    private sealed class FakeStateStore : IStateStore
    {
        private readonly Dictionary<QueryId, QueryState> _states = new();

        public Task<QueryState> GetOrCreateAsync(QueryId queryId, WatermarkKind kind, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            if (!_states.TryGetValue(queryId, out var state))
            {
                state = new QueryState
                {
                    QueryId = queryId,
                    Watermark = kind == WatermarkKind.Time
                        ? Watermark.ForTime(nowUtc.AddMinutes(-10))
                        : Watermark.ForIndex(0),
                    LastSuccessUtc = null,
                    LastAttemptUtc = null,
                    ConsecutiveFailures = 0,
                    NextRetryUtc = null,
                    LastErrorSummary = null,
                };
                _states[queryId] = state;
            }
            return Task.FromResult(state);
        }

        public Task RecordAttemptAsync(RunPlan plan, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            if (_states.TryGetValue(plan.QueryId, out var state))
            {
                _states[plan.QueryId] = state with { LastAttemptUtc = nowUtc };
            }
            return Task.CompletedTask;
        }

        public Task RecordSuccessAndAdvanceWatermarkAsync(RunPlan plan, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            _states[plan.QueryId] = new QueryState
            {
                QueryId = plan.QueryId,
                Watermark = plan.CandidateWatermarkExclusiveEnd,
                LastSuccessUtc = nowUtc,
                LastAttemptUtc = nowUtc,
                ConsecutiveFailures = 0,
            };
            return Task.CompletedTask;
        }

        public Task RecordFailureAsync(RunPlan plan, string? errorSummary, bool retryable, DateTimeOffset nowUtc, DateTimeOffset? nextRetryUtc, CancellationToken cancellationToken)
        {
            if (_states.TryGetValue(plan.QueryId, out var state))
            {
                _states[plan.QueryId] = state with
                {
                    LastAttemptUtc = nowUtc,
                    ConsecutiveFailures = state.ConsecutiveFailures + 1,
                    NextRetryUtc = nextRetryUtc,
                    LastErrorSummary = errorSummary,
                };
            }
            return Task.CompletedTask;
        }

        public Task ResetWatermarkAsync(QueryId queryId, Watermark newWatermark, CancellationToken cancellationToken)
        {
            if (_states.TryGetValue(queryId, out var state))
            {
                _states[queryId] = state with { Watermark = newWatermark, ConsecutiveFailures = 0, NextRetryUtc = null };
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeExecutor : IHostQueryExecutor
    {
        public int ExecutionCount { get; private set; }

        public async IAsyncEnumerable<NormalizedRecord> ExecuteAsync(
            QueryDefinition query,
            QueryRange range,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ExecutionCount++;
            await Task.Yield();
            yield break;
        }
    }

    private sealed class FakeResultHandler : IHostResultsetHandler
    {
        public Task<AckResult> HandleAsync(TimeSeriesBatch resultset, MappingConfiguration mappingConfiguration, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AckResult(Success: true));
        }
    }

    private sealed class FakeMappingProvider : IMappingConfigurationProvider
    {
        public MappingConfiguration GetMapping(QueryId queryId) => new MappingConfiguration(null);
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset initialTime) => UtcNow = initialTime;
        public DateTimeOffset UtcNow { get; set; }
    }
}
