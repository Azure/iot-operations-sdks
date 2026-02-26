// <copyright file="RqrEngineConcurrencyTests.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit;

namespace ResilientQueryRunner.Tests;

/// <summary>
/// Tests for RqrEngine concurrency and thread-safety scenarios.
/// </summary>
public sealed class RqrEngineConcurrencyTests
{
    private readonly DateTimeOffset _baseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TickAsync_WithMultipleConcurrentTicks_HandlesGracefully()
    {
        // Arrange
        var clock = new TestClock(_baseTime);
        var stateStore = new InMemoryStateStore();
        var executor = new SlowQueryExecutor(TimeSpan.FromMilliseconds(100));
        var resultsetHandler = new TestResultsetHandler();
        var mappingProvider = new TestMappingProvider();

        using var engine = new RqrEngine(
            "concurrent-ticks-connector",
            stateStore,
            executor,
            resultsetHandler,
            mappingProvider,
            globalConcurrencyLimit: 2,
            clock: clock);

        var query = new QueryDefinition
        {
            QueryId = new QueryId("concurrent-query"),
            Cron = CronExpression.Parse("0 0 * * * *"),
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = TimeSpan.FromHours(1),
            },
        };

        engine.RegisterQuery(query);

        // Request multiple adhoc runs to ensure work is available
        for (var i = 0; i < 3; i++)
        {
            engine.RequestAdhocRun(
                query.QueryId,
                QueryRange.Time(_baseTime.AddHours(-i - 1), _baseTime.AddHours(-i)),
                advanceWatermark: false);
        }

        // Act - Run multiple ticks concurrently
        var tasks = new List<Task>();
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(engine.TickAsync(CancellationToken.None));
            await Task.Delay(10); // Stagger starts slightly
        }

        await Task.WhenAll(tasks);

        // Assert - Should complete without errors
        executor.ExecutionCount.Should().BeGreaterThan(0);
        resultsetHandler.HandledBatches.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TickAsync_WithPerQueryConcurrencyLimit_EnforcesOneAtATime()
    {
        // Arrange
        var clock = new TestClock(_baseTime);
        var stateStore = new InMemoryStateStore();
        var executor = new ConcurrencyTrackingExecutor();
        var resultsetHandler = new TestResultsetHandler();
        var mappingProvider = new TestMappingProvider();

        using var engine = new RqrEngine(
            "per-query-limit-connector",
            stateStore,
            executor,
            resultsetHandler,
            mappingProvider,
            globalConcurrencyLimit: 10, // High global limit
            clock: clock);

        var query = new QueryDefinition
        {
            QueryId = new QueryId("sequential-query"),
            Cron = CronExpression.Parse("0 0 * * * *"),
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = TimeSpan.FromHours(1),
                CatchUpMaxWindowsPerTick = 3, // Multiple windows
            },
        };

        engine.RegisterQuery(query);

        // Initialize state first
        await stateStore.GetOrCreateAsync(query.QueryId, query.WatermarkKind, _baseTime, CancellationToken.None);

        // Set watermark far behind to create multiple windows
        await stateStore.RecordSuccessAndAdvanceWatermarkAsync(
            new RunPlan
            {
                RunId = RunId.New(),
                QueryId = query.QueryId,
                Range = QueryRange.Time(_baseTime.AddHours(-10), _baseTime.AddHours(-10)),
                CandidateWatermarkExclusiveEnd = Watermark.ForTime(_baseTime.AddHours(-10)),
                AdvanceWatermark = true,
            },
            _baseTime,
            CancellationToken.None);

        // Act - Execute multiple ticks
        for (var i = 0; i < 5; i++)
        {
            await engine.TickAsync(CancellationToken.None);
        }

        // Assert - Max concurrent executions for same query should be 1
        executor.MaxConcurrentExecutions.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task RegisterQuery_CalledConcurrently_HandlesSafely()
    {
        // Arrange
        var clock = new TestClock(_baseTime);
        var stateStore = new InMemoryStateStore();
        var executor = new TestQueryExecutor();
        var resultsetHandler = new TestResultsetHandler();
        var mappingProvider = new TestMappingProvider();

        using var engine = new RqrEngine(
            "concurrent-register-connector",
            stateStore,
            executor,
            resultsetHandler,
            mappingProvider,
            clock: clock);

        // Act - Register multiple queries concurrently
        var tasks = Enumerable.Range(0, 10).Select(i =>
            Task.Run(() =>
            {
                var query = new QueryDefinition
                {
                    QueryId = new QueryId($"query-{i}"),
                    Cron = CronExpression.Parse("0 0 * * * *"),
                    WatermarkKind = WatermarkKind.Time,
                    RangeParameters = new RangeParameters
                    {
                        WindowDuration = TimeSpan.FromHours(1),
                    },
                };
                engine.RegisterQuery(query);
            })).ToList();

        await Task.WhenAll(tasks);

        // Assert - Should complete without errors
        tasks.Should().OnlyContain(t => t.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task RequestAdhocRun_CalledConcurrently_EnqueuesAllRuns()
    {
        // Arrange
        var clock = new TestClock(_baseTime);
        var stateStore = new InMemoryStateStore();
        var executor = new TestQueryExecutor();
        var resultsetHandler = new TestResultsetHandler();
        var mappingProvider = new TestMappingProvider();

        using var engine = new RqrEngine(
            "concurrent-adhoc-connector",
            stateStore,
            executor,
            resultsetHandler,
            mappingProvider,
            clock: clock);

        var query = new QueryDefinition
        {
            QueryId = new QueryId("adhoc-query"),
            Cron = CronExpression.Parse("0 0 * * * *"),
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = TimeSpan.FromHours(1),
            },
        };

        engine.RegisterQuery(query);

        // Act - Request multiple adhoc runs concurrently
        var tasks = Enumerable.Range(0, 20).Select(i =>
            Task.Run(() =>
            {
                var range = QueryRange.Time(_baseTime.AddHours(-i - 1), _baseTime.AddHours(-i));
                engine.RequestAdhocRun(query.QueryId, range, advanceWatermark: false);
            }));

        await Task.WhenAll(tasks);

        // Execute ticks to process all adhoc runs
        for (var i = 0; i < 25; i++)
        {
            await engine.TickAsync(CancellationToken.None);
        }

        // Assert - Should have executed all adhoc runs
        executor.ExecutionCount.Should().BeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public async Task TickAsync_WithSimultaneousFailureAndSuccess_HandlesCorrectly()
    {
        // Arrange
        var clock = new TestClock(_baseTime);
        var stateStore = new InMemoryStateStore();
        var executor = new MixedResultExecutor();
        var resultsetHandler = new TestResultsetHandler();
        var mappingProvider = new TestMappingProvider();

        using var engine = new RqrEngine(
            "mixed-results-connector",
            stateStore,
            executor,
            resultsetHandler,
            mappingProvider,
            globalConcurrencyLimit: 5,
            clock: clock);

        // Register multiple queries and request adhoc runs
        for (var i = 0; i < 5; i++)
        {
            var query = new QueryDefinition
            {
                QueryId = new QueryId($"mixed-query-{i}"),
                Cron = CronExpression.Parse("0 0 * * * *"),
                WatermarkKind = WatermarkKind.Time,
                RangeParameters = new RangeParameters
                {
                    WindowDuration = TimeSpan.FromHours(1),
                },
            };

            engine.RegisterQuery(query);

            // Request adhoc run for each query
            engine.RequestAdhocRun(
                query.QueryId,
                QueryRange.Time(_baseTime.AddHours(-1), _baseTime),
                advanceWatermark: false);
        }

        // Act
        await engine.TickAsync(CancellationToken.None);

        // Assert - All queries should be attempted
        executor.ExecutionCount.Should().Be(5);
    }

    [Fact]
    public async Task Dispose_WhileTickRunning_CompletesGracefully()
    {
        // Arrange
        var clock = new TestClock(_baseTime);
        var stateStore = new InMemoryStateStore();
        var executor = new SlowQueryExecutor(TimeSpan.FromSeconds(2));
        var resultsetHandler = new TestResultsetHandler();
        var mappingProvider = new TestMappingProvider();

        var engine = new RqrEngine(
            "dispose-during-tick-connector",
            stateStore,
            executor,
            resultsetHandler,
            mappingProvider,
            clock: clock);

        var query = new QueryDefinition
        {
            QueryId = new QueryId("slow-query"),
            Cron = CronExpression.Parse("0 0 * * * *"),
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = TimeSpan.FromHours(1),
            },
        };

        engine.RegisterQuery(query);

        // Initialize state first
        await stateStore.GetOrCreateAsync(query.QueryId, query.WatermarkKind, _baseTime, CancellationToken.None);

        await stateStore.RecordSuccessAndAdvanceWatermarkAsync(
            new RunPlan
            {
                RunId = RunId.New(),
                QueryId = query.QueryId,
                Range = QueryRange.Time(_baseTime.AddHours(-2), _baseTime.AddHours(-2)),
                CandidateWatermarkExclusiveEnd = Watermark.ForTime(_baseTime.AddHours(-2)),
                AdvanceWatermark = true,
            },
            _baseTime,
            CancellationToken.None);

        // Act - Start tick and dispose while running
        var tickTask = Task.Run(async () =>
        {
            try
            {
                await engine.TickAsync(CancellationToken.None);
            }
            catch (ObjectDisposedException)
            {
                // Expected
            }
        });

        await Task.Delay(100); // Let tick start
        engine.Dispose();

        // Assert - Should complete without hanging
        var completedInTime = await Task.WhenAny(tickTask, Task.Delay(TimeSpan.FromSeconds(5))) == tickTask;
        completedInTime.Should().BeTrue("Dispose should not hang");
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
            await Task.Yield();

            yield return new NormalizedRecord
            {
                SeriesId = "test-series",
                TimestampUtc = range.FromTimeUtc!.Value,
                Value = 42.0,
            };
        }
    }

    private sealed class SlowQueryExecutor : IHostQueryExecutor
    {
        private readonly TimeSpan _delay;
        private int _executionCount;

        public int ExecutionCount => _executionCount;

        public SlowQueryExecutor(TimeSpan delay)
        {
            _delay = delay;
        }

        public async IAsyncEnumerable<NormalizedRecord> ExecuteAsync(
            QueryDefinition query,
            QueryRange range,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _executionCount);
            await Task.Delay(_delay, cancellationToken);

            yield return new NormalizedRecord
            {
                SeriesId = "slow-series",
                TimestampUtc = range.FromTimeUtc!.Value,
                Value = 42.0,
            };
        }
    }

    private sealed class ConcurrencyTrackingExecutor : IHostQueryExecutor
    {
        private int _currentConcurrency;
        private int _maxConcurrency;

        public int MaxConcurrentExecutions => _maxConcurrency;

        public async IAsyncEnumerable<NormalizedRecord> ExecuteAsync(
            QueryDefinition query,
            QueryRange range,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref _currentConcurrency);
            
            // Update max if needed
            var spinWait = new SpinWait();
            while (true)
            {
                var max = _maxConcurrency;
                if (current <= max || Interlocked.CompareExchange(ref _maxConcurrency, current, max) == max)
                {
                    break;
                }
                spinWait.SpinOnce();
            }

            try
            {
                await Task.Delay(50, cancellationToken); // Simulate work

                yield return new NormalizedRecord
                {
                    SeriesId = "tracking-series",
                    TimestampUtc = range.FromTimeUtc!.Value,
                    Value = 42.0,
                };
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrency);
            }
        }
    }

    private sealed class MixedResultExecutor : IHostQueryExecutor
    {
        private int _executionCount;

        public int ExecutionCount => _executionCount;

        public async IAsyncEnumerable<NormalizedRecord> ExecuteAsync(
            QueryDefinition query,
            QueryRange range,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref _executionCount);

            // Fail every 3rd execution
            if (count % 3 == 0)
            {
                throw new Exception($"Simulated failure for execution {count}");
            }

            await Task.Yield();

            yield return new NormalizedRecord
            {
                SeriesId = query.QueryId.Value,
                TimestampUtc = range.FromTimeUtc!.Value,
                Value = count * 10.0,
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
            lock (_handledBatches)
            {
                _handledBatches.Add(resultset);
            }
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
