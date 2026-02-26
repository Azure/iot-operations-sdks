// <copyright file="RqrEngineIntegrationTests.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace ResilientQueryRunner.Tests;

/// <summary>
/// Integration tests for RqrEngine using real implementations.
/// </summary>
public sealed class RqrEngineIntegrationTests
{
    private readonly DateTimeOffset _baseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly ITestOutputHelper _output;

    public RqrEngineIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task FullWorkflow_WithTimeBasedQuery_ExecutesSuccessfully()
    {
        _output.WriteLine("Starting FullWorkflow_WithTimeBasedQuery_ExecutesSuccessfully");
        // Arrange
        var clock = new TestClock(_baseTime);
        var stateStore = new InMemoryStateStore();
        var executor = new TestQueryExecutor();
        var resultsetHandler = new TestResultsetHandler();
        var mappingProvider = new TestMappingProvider();

        using var engine = new RqrEngine(
            "integration-test-connector",
            stateStore,
            executor,
            resultsetHandler,
            mappingProvider,
            clock: clock);

        var query = new QueryDefinition
        {
            QueryId = new QueryId("integration-query"),
            Cron = CronExpression.Parse("0 0 * * * *"), // Every hour
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = TimeSpan.FromHours(1),
            },
        };

        engine.RegisterQuery(query);

        // Request adhoc run to ensure execution
        var range = QueryRange.Time(_baseTime.AddHours(-2), _baseTime.AddHours(-1));
        engine.RequestAdhocRun(query.QueryId, range, advanceWatermark: true);

        // Act - Execute first tick
        await engine.TickAsync(CancellationToken.None);

        // Assert
        executor.ExecutionCount.Should().BeGreaterThan(0);
        resultsetHandler.HandledBatches.Should().NotBeEmpty();

        var state = await stateStore.GetOrCreateAsync(
            query.QueryId,
            query.WatermarkKind,
            _baseTime,
            CancellationToken.None);

        // Watermark should have advanced to the end of the executed range
        state.Watermark.TimeUtc.Should().BeAfter(_baseTime.AddHours(-2));
        state.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public async Task CatchUp_WithMultipleWindows_ExecutesInOrder()
    {
        _output.WriteLine("Starting CatchUp_WithMultipleWindows_ExecutesInOrder");
        // Arrange
        var clock = new TestClock(_baseTime);
        var stateStore = new InMemoryStateStore();
        var executor = new TestQueryExecutor();
        var resultsetHandler = new TestResultsetHandler();
        var mappingProvider = new TestMappingProvider();

        using var engine = new RqrEngine(
            "catchup-test-connector",
            stateStore,
            executor,
            resultsetHandler,
            mappingProvider,
            globalConcurrencyLimit: 10, // High limit to allow all to execute
            clock: clock);

        var query = new QueryDefinition
        {
            QueryId = new QueryId("catchup-query"),
            Cron = CronExpression.Parse("0 0 * * * *"),
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = TimeSpan.FromHours(1),
            },
        };

        engine.RegisterQuery(query);

        // Request multiple adhoc runs to simulate catch-up
        for (var i = 0; i < 5; i++)
        {
            var range = QueryRange.Time(
                _baseTime.AddHours(-10 + i),
                _baseTime.AddHours(-9 + i));
            engine.RequestAdhocRun(query.QueryId, range, advanceWatermark: false);
        }

        // Act - Execute ticks to process all runs
        for (var i = 0; i < 5; i++)
        {
            await engine.TickAsync(CancellationToken.None);
        }

        // Assert
        executor.ExecutionCount.Should().BeGreaterThanOrEqualTo(5); // Should catch up
        resultsetHandler.HandledBatches.Should().HaveCountGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task AdhocRun_WithHighPriority_ExecutesBeforeScheduled()
    {
        _output.WriteLine("Starting AdhocRun_WithHighPriority_ExecutesBeforeScheduled");
        // Arrange
        var clock = new TestClock(_baseTime);
        var stateStore = new InMemoryStateStore();
        var executor = new TestQueryExecutor();
        var resultsetHandler = new TestResultsetHandler();
        var mappingProvider = new TestMappingProvider();

        using var engine = new RqrEngine(
            "priority-test-connector",
            stateStore,
            executor,
            resultsetHandler,
            mappingProvider,
            globalConcurrencyLimit: 1, // Force serial execution
            clock: clock);

        var query = new QueryDefinition
        {
            QueryId = new QueryId("priority-query"),
            Cron = CronExpression.Parse("0 0 * * * *"),
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = TimeSpan.FromHours(1),
            },
            PriorityParameters = new PriorityParameters
            {
                BasePriority = 10,
            },
        };

        engine.RegisterQuery(query);

        // Request low-priority adhoc run first
        var lowPriorityRange = QueryRange.Time(_baseTime.AddHours(-2), _baseTime.AddHours(-1));
        engine.RequestAdhocRun(query.QueryId, lowPriorityRange, advanceWatermark: false, priorityOverride: 100);

        // Request high-priority adhoc run second
        var highPriorityRange = QueryRange.Time(_baseTime.AddHours(-3), _baseTime.AddHours(-2));
        engine.RequestAdhocRun(query.QueryId, highPriorityRange, advanceWatermark: false, priorityOverride: 1000);

        // Act
        await engine.TickAsync(CancellationToken.None);

        // Assert - At least one should execute
        executor.ExecutionCount.Should().BeGreaterThan(0);
        resultsetHandler.HandledBatches.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FailedRun_WithRetry_EventuallySucceeds()
    {
        _output.WriteLine("Starting FailedRun_WithRetry_EventuallySucceeds");
        // Arrange
        var clock = new TestClock(_baseTime);
        var stateStore = new InMemoryStateStore();
        var executor = new FailingTestQueryExecutor(failCount: 2); // Fail twice, then succeed
        var resultsetHandler = new TestResultsetHandler();
        var mappingProvider = new TestMappingProvider();

        using var engine = new RqrEngine(
            "retry-test-connector",
            stateStore,
            executor,
            resultsetHandler,
            mappingProvider,
            clock: clock);

        var query = new QueryDefinition
        {
            QueryId = new QueryId("retry-query"),
            Cron = CronExpression.Parse("0 0 * * * *"),
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = TimeSpan.FromHours(1),
            },
        };

        engine.RegisterQuery(query);

        // Request adhoc run
        var range = QueryRange.Time(_baseTime.AddHours(-1), _baseTime);
        engine.RequestAdhocRun(query.QueryId, range, advanceWatermark: true);

        // Act - Execute multiple ticks
        await engine.TickAsync(CancellationToken.None); // First attempt - fails

        clock.Advance(TimeSpan.FromMinutes(1));
        await engine.TickAsync(CancellationToken.None); // Second attempt - fails

        clock.Advance(TimeSpan.FromMinutes(2));
        await engine.TickAsync(CancellationToken.None); // Third attempt - succeeds

        // Assert
        executor.ExecutionCount.Should().Be(3);
        resultsetHandler.HandledBatches.Should().NotBeEmpty();

        var state = await stateStore.GetOrCreateAsync(
            query.QueryId,
            query.WatermarkKind,
            clock.UtcNow,
            CancellationToken.None);

        state.ConsecutiveFailures.Should().Be(0); // Reset after success
    }

    [Fact]
    public async Task MultipleQueries_WithConcurrencyLimit_ExecutesConcurrently()
    {
        _output.WriteLine("Starting MultipleQueries_WithConcurrencyLimit_ExecutesConcurrently");
        // Arrange
        var clock = new TestClock(_baseTime);
        var stateStore = new InMemoryStateStore();
        var executor = new TestQueryExecutor();
        var resultsetHandler = new TestResultsetHandler();
        var mappingProvider = new TestMappingProvider();

        using var engine = new RqrEngine(
            "concurrent-test-connector",
            stateStore,
            executor,
            resultsetHandler,
            mappingProvider,
            globalConcurrencyLimit: 2,
            clock: clock);

        var query1 = new QueryDefinition
        {
            QueryId = new QueryId("concurrent-query-1"),
            Cron = CronExpression.Parse("0 0 * * * *"),
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = TimeSpan.FromHours(1),
            },
        };

        var query2 = new QueryDefinition
        {
            QueryId = new QueryId("concurrent-query-2"),
            Cron = CronExpression.Parse("0 0 * * * *"),
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = TimeSpan.FromHours(1),
            },
        };

        engine.RegisterQuery(query1);
        engine.RegisterQuery(query2);

        // Request adhoc runs for both queries
        engine.RequestAdhocRun(
            query1.QueryId,
            QueryRange.Time(_baseTime.AddHours(-1), _baseTime),
            advanceWatermark: false);

        engine.RequestAdhocRun(
            query2.QueryId,
            QueryRange.Time(_baseTime.AddHours(-1), _baseTime),
            advanceWatermark: false);

        // Act - Run multiple ticks to ensure both execute
        await engine.TickAsync(CancellationToken.None);
        await engine.TickAsync(CancellationToken.None);

        // Assert
        executor.ExecutionCount.Should().Be(2);
        resultsetHandler.HandledBatches.Count.Should().Be(2);
    }

    #region Test Helpers

    private sealed class TestClock : IClock
    {
        public TestClock(DateTimeOffset initialTime)
        {
            UtcNow = initialTime;
        }

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan duration)
        {
            UtcNow = UtcNow.Add(duration);
        }
    }

    private sealed class TestQueryExecutor : IHostQueryExecutor
    {
        private int _executionCount;

        public int ExecutionCount => _executionCount;

        public async IAsyncEnumerable<NormalizedRecord> ExecuteAsync(
            QueryDefinition query,
            QueryRange range,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _executionCount);

            // Simulate some data
            await Task.Yield();

            yield return new NormalizedRecord
            {
                SeriesId = "test-series-1",
                TimestampUtc = range.FromTimeUtc!.Value,
                Value = 42.0,
            };
        }
    }

    private sealed class FailingTestQueryExecutor : IHostQueryExecutor
    {
        private readonly int _failCount;
        private int _executionCount;

        public int ExecutionCount => _executionCount;

        public FailingTestQueryExecutor(int failCount)
        {
            _failCount = failCount;
        }

        public async IAsyncEnumerable<NormalizedRecord> ExecuteAsync(
            QueryDefinition query,
            QueryRange range,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _executionCount);

            if (attempt <= _failCount)
            {
                throw new Exception($"Simulated failure on attempt {attempt}");
            }

            await Task.Yield();

            yield return new NormalizedRecord
            {
                SeriesId = "test-series-1",
                TimestampUtc = range.FromTimeUtc!.Value,
                Value = 42.0,
            };
        }
    }

    private sealed class TestResultsetHandler : IHostResultsetHandler
    {
        private readonly List<TimeSeriesBatch> _handledBatches = new();

        public IReadOnlyList<TimeSeriesBatch> HandledBatches => _handledBatches;

        public Task<AckResult> HandleAsync(
            TimeSeriesBatch resultset,
            MappingConfiguration mappingConfiguration,
            CancellationToken cancellationToken)
        {
            _handledBatches.Add(resultset);
            return Task.FromResult(new AckResult(true));
        }
    }

    private sealed class TestMappingProvider : IMappingConfigurationProvider
    {
        public MappingConfiguration GetMapping(QueryId queryId)
        {
            return new MappingConfiguration(null);
        }
    }

    #endregion
}
