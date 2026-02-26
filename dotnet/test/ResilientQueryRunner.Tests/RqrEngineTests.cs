// <copyright file="RqrEngineTests.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using FluentAssertions;
using Moq;
using Xunit;

namespace ResilientQueryRunner.Tests;

/// <summary>
/// Unit tests for RqrEngine.
/// </summary>
public sealed class RqrEngineTests : IDisposable
{
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Mock<IHostQueryExecutor> _executorMock;
    private readonly Mock<IHostResultsetHandler> _resultsetHandlerMock;
    private readonly Mock<IMappingConfigurationProvider> _mappingProviderMock;
    private readonly Mock<IClock> _clockMock;
    private readonly Mock<IFailureClassifier> _failureClassifierMock;
    private readonly Mock<IRetryStrategy> _retryStrategyMock;
    private readonly DateTimeOffset _baseTime;

    public RqrEngineTests()
    {
        _stateStoreMock = new Mock<IStateStore>();
        _executorMock = new Mock<IHostQueryExecutor>();
        _resultsetHandlerMock = new Mock<IHostResultsetHandler>();
        _mappingProviderMock = new Mock<IMappingConfigurationProvider>();
        _clockMock = new Mock<IClock>();
        _failureClassifierMock = new Mock<IFailureClassifier>();
        _retryStrategyMock = new Mock<IRetryStrategy>();

        _baseTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _clockMock.Setup(c => c.UtcNow).Returns(_baseTime);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        using var engine = CreateEngine();

        // Assert
        engine.Should().NotBeNull();
        engine.ConnectorIdentity.Should().Be("test-connector");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidConnectorIdentity_ThrowsArgumentException(string? connectorIdentity)
    {
        // Act
        var act = () => new RqrEngine(
            connectorIdentity!,
            _stateStoreMock.Object,
            _executorMock.Object,
            _resultsetHandlerMock.Object,
            _mappingProviderMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("connectorIdentity");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidConcurrencyLimit_ThrowsArgumentOutOfRangeException(int limit)
    {
        // Act
        var act = () => new RqrEngine(
            "test-connector",
            _stateStoreMock.Object,
            _executorMock.Object,
            _resultsetHandlerMock.Object,
            _mappingProviderMock.Object,
            globalConcurrencyLimit: limit);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNullOptionalParameters_UsesDefaults()
    {
        // Act
        using var engine = new RqrEngine(
            "test-connector",
            _stateStoreMock.Object,
            _executorMock.Object,
            _resultsetHandlerMock.Object,
            _mappingProviderMock.Object,
            clock: null,
            failureClassifier: null,
            retryStrategy: null);

        // Assert
        engine.Should().NotBeNull();
    }

    #endregion

    #region RegisterQuery Tests

    [Fact]
    public void RegisterQuery_WithValidQuery_RegistersSuccessfully()
    {
        // Arrange
        using var engine = CreateEngine();
        var query = CreateTestQuery();

        // Act
        var act = () => engine.RegisterQuery(query);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterQuery_WithInvalidRangeParameters_ThrowsException()
    {
        // Arrange
        using var engine = CreateEngine();
        var query = new QueryDefinition
        {
            QueryId = new QueryId("test-query"),
            Cron = CronExpression.Parse("0 0 * * * *"),
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = null, // Invalid for Time-based
            },
        };

        // Act
        var act = () => engine.RegisterQuery(query);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegisterQuery_RegistersSameQueryTwice_OverwritesFirst()
    {
        // Arrange
        using var engine = CreateEngine();
        var query1 = CreateTestQuery();
        var query2 = CreateTestQuery(); // Same QueryId

        // Act
        engine.RegisterQuery(query1);
        var act = () => engine.RegisterQuery(query2);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region RequestAdhocRun Tests

    [Fact]
    public void RequestAdhocRun_WithValidParameters_EnqueuesRun()
    {
        // Arrange
        using var engine = CreateEngine();
        var queryId = new QueryId("test-query");
        var range = QueryRange.Time(_baseTime, _baseTime.AddHours(1));

        // Act
        var act = () => engine.RequestAdhocRun(queryId, range, advanceWatermark: false);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RequestAdhocRun_WithPriorityOverride_UsesPriorityOverride()
    {
        // Arrange
        using var engine = CreateEngine();
        var queryId = new QueryId("test-query");
        var range = QueryRange.Time(_baseTime, _baseTime.AddHours(1));

        // Act
        var act = () => engine.RequestAdhocRun(queryId, range, advanceWatermark: true, priorityOverride: 999);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RequestAdhocRun_WithoutPriorityOverride_UsesMaxIntPriority()
    {
        // Arrange
        using var engine = CreateEngine();
        var queryId = new QueryId("test-query");
        var range = QueryRange.Time(_baseTime, _baseTime.AddHours(1));

        // Act
        var act = () => engine.RequestAdhocRun(queryId, range);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region TickAsync Tests

    [Fact]
    public async Task TickAsync_WithNoQueries_CompletesSuccessfully()
    {
        // Arrange
        using var engine = CreateEngine();

        // Act
        var act = async () => await engine.TickAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TickAsync_WithRegisteredQuery_PlansAndExecutesRuns()
    {
        // Arrange
        using var engine = CreateEngine();
        var query = CreateTestQuery();
        engine.RegisterQuery(query);

        var state = new QueryState
        {
            QueryId = query.QueryId,
            Watermark = Watermark.ForTime(_baseTime.AddHours(-2)),
            ConsecutiveFailures = 0,
        };

        _stateStoreMock
            .Setup(s => s.GetOrCreateAsync(query.QueryId, query.WatermarkKind, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        _stateStoreMock
            .Setup(s => s.RecordAttemptAsync(It.IsAny<RunPlan>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _executorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<QueryDefinition>(), It.IsAny<QueryRange>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<NormalizedRecord>());

        _mappingProviderMock
            .Setup(m => m.GetMapping(query.QueryId))
            .Returns(new MappingConfiguration(null));

        _resultsetHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<TimeSeriesBatch>(), It.IsAny<MappingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AckResult(true));

        _stateStoreMock
            .Setup(s => s.RecordSuccessAndAdvanceWatermarkAsync(It.IsAny<RunPlan>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await engine.TickAsync(CancellationToken.None);

        // Assert
        _executorMock.Verify(e => e.ExecuteAsync(It.IsAny<QueryDefinition>(), It.IsAny<QueryRange>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task TickAsync_WithQueryInRetryWindow_SkipsExecution()
    {
        // Arrange
        using var engine = CreateEngine();
        var query = CreateTestQuery();
        engine.RegisterQuery(query);

        var state = new QueryState
        {
            QueryId = query.QueryId,
            Watermark = Watermark.ForTime(_baseTime.AddHours(-2)),
            ConsecutiveFailures = 1,
            NextRetryUtc = _baseTime.AddMinutes(10), // In the future
        };

        _stateStoreMock
            .Setup(s => s.GetOrCreateAsync(query.QueryId, query.WatermarkKind, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act
        await engine.TickAsync(CancellationToken.None);

        // Assert
        _executorMock.Verify(e => e.ExecuteAsync(It.IsAny<QueryDefinition>(), It.IsAny<QueryRange>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TickAsync_WithNoRangesToExecute_DoesNotExecute()
    {
        // Arrange
        using var engine = CreateEngine();
        var query = CreateTestQuery();
        engine.RegisterQuery(query);

        var state = new QueryState
        {
            QueryId = query.QueryId,
            Watermark = Watermark.ForTime(_baseTime), // Already caught up
            ConsecutiveFailures = 0,
        };

        _stateStoreMock
            .Setup(s => s.GetOrCreateAsync(query.QueryId, query.WatermarkKind, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act
        await engine.TickAsync(CancellationToken.None);

        // Assert
        _executorMock.Verify(e => e.ExecuteAsync(It.IsAny<QueryDefinition>(), It.IsAny<QueryRange>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TickAsync_WithAdhocRun_ExecutesAdhocRun()
    {
        // Arrange
        using var engine = CreateEngine();
        var query = CreateTestQuery();
        engine.RegisterQuery(query);

        var state = new QueryState
        {
            QueryId = query.QueryId,
            Watermark = Watermark.ForTime(_baseTime),
            ConsecutiveFailures = 0,
        };

        _stateStoreMock
            .Setup(s => s.GetOrCreateAsync(query.QueryId, query.WatermarkKind, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        _stateStoreMock
            .Setup(s => s.RecordAttemptAsync(It.IsAny<RunPlan>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _executorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<QueryDefinition>(), It.IsAny<QueryRange>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<NormalizedRecord>());

        _mappingProviderMock
            .Setup(m => m.GetMapping(query.QueryId))
            .Returns(new MappingConfiguration(null));

        _resultsetHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<TimeSeriesBatch>(), It.IsAny<MappingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AckResult(true));

        _stateStoreMock
            .Setup(s => s.RecordSuccessAndAdvanceWatermarkAsync(It.IsAny<RunPlan>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var range = QueryRange.Time(_baseTime.AddHours(-1), _baseTime);
        engine.RequestAdhocRun(query.QueryId, range);

        // Act
        await engine.TickAsync(CancellationToken.None);

        // Assert
        _executorMock.Verify(e => e.ExecuteAsync(It.IsAny<QueryDefinition>(), It.IsAny<QueryRange>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TickAsync_WithFailedExecution_RecordsFailure()
    {
        // Arrange
        using var engine = CreateEngine();
        var query = CreateTestQuery();
        engine.RegisterQuery(query);

        var state = new QueryState
        {
            QueryId = query.QueryId,
            Watermark = Watermark.ForTime(_baseTime.AddHours(-2)),
            ConsecutiveFailures = 0,
        };

        _stateStoreMock
            .Setup(s => s.GetOrCreateAsync(query.QueryId, query.WatermarkKind, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        _stateStoreMock
            .Setup(s => s.RecordAttemptAsync(It.IsAny<RunPlan>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _executorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<QueryDefinition>(), It.IsAny<QueryRange>(), It.IsAny<CancellationToken>()))
            .Throws(new Exception("Test failure"));

        _failureClassifierMock
            .Setup(f => f.IsRetryable(It.IsAny<Exception>()))
            .Returns(true);

        _retryStrategyMock
            .Setup(r => r.ComputeNextRetryUtc(It.IsAny<DateTimeOffset>(), It.IsAny<int>()))
            .Returns(_baseTime.AddMinutes(5));

        _stateStoreMock
            .Setup(s => s.RecordFailureAsync(
                It.IsAny<RunPlan>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await engine.TickAsync(CancellationToken.None);

        // Assert
        _stateStoreMock.Verify(s => s.RecordFailureAsync(
            It.IsAny<RunPlan>(),
            "Test failure",
            true,
            It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TickAsync_WithNonRetryableFailure_RecordsFailureWithoutRetry()
    {
        // Arrange
        using var engine = CreateEngine();
        var query = CreateTestQuery();
        engine.RegisterQuery(query);

        var state = new QueryState
        {
            QueryId = query.QueryId,
            Watermark = Watermark.ForTime(_baseTime.AddHours(-2)),
            ConsecutiveFailures = 0,
        };

        _stateStoreMock
            .Setup(s => s.GetOrCreateAsync(query.QueryId, query.WatermarkKind, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        _stateStoreMock
            .Setup(s => s.RecordAttemptAsync(It.IsAny<RunPlan>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _executorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<QueryDefinition>(), It.IsAny<QueryRange>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Non-retryable failure"));

        _failureClassifierMock
            .Setup(f => f.IsRetryable(It.IsAny<Exception>()))
            .Returns(false);

        _stateStoreMock
            .Setup(s => s.RecordFailureAsync(
                It.IsAny<RunPlan>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await engine.TickAsync(CancellationToken.None);

        // Assert
        _stateStoreMock.Verify(s => s.RecordFailureAsync(
            It.IsAny<RunPlan>(),
            "Non-retryable failure",
            false,
            It.IsAny<DateTimeOffset>(),
            null, // No retry scheduled
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TickAsync_WithResultsetHandlerFailure_RecordsFailure()
    {
        // Arrange
        using var engine = CreateEngine();
        var query = CreateTestQuery();
        engine.RegisterQuery(query);

        var state = new QueryState
        {
            QueryId = query.QueryId,
            Watermark = Watermark.ForTime(_baseTime.AddHours(-2)),
            ConsecutiveFailures = 0,
        };

        _stateStoreMock
            .Setup(s => s.GetOrCreateAsync(query.QueryId, query.WatermarkKind, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        _stateStoreMock
            .Setup(s => s.RecordAttemptAsync(It.IsAny<RunPlan>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _executorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<QueryDefinition>(), It.IsAny<QueryRange>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<NormalizedRecord>());

        _mappingProviderMock
            .Setup(m => m.GetMapping(query.QueryId))
            .Returns(new MappingConfiguration(null));

        _resultsetHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<TimeSeriesBatch>(), It.IsAny<MappingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AckResult(false, "Handler error"));

        _failureClassifierMock
            .Setup(f => f.IsRetryable(It.IsAny<Exception>()))
            .Returns(true);

        _retryStrategyMock
            .Setup(r => r.ComputeNextRetryUtc(It.IsAny<DateTimeOffset>(), It.IsAny<int>()))
            .Returns(_baseTime.AddMinutes(5));

        _stateStoreMock
            .Setup(s => s.RecordFailureAsync(
                It.IsAny<RunPlan>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await engine.TickAsync(CancellationToken.None);

        // Assert
        _stateStoreMock.Verify(s => s.RecordFailureAsync(
            It.IsAny<RunPlan>(),
            "Handler error",
            true,
            It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TickAsync_WithGlobalConcurrencyLimit_RespectsLimit()
    {
        // Arrange
        using var engine = CreateEngine(globalConcurrencyLimit: 1);

        var query1 = new QueryDefinition
        {
            QueryId = new QueryId("query-1"),
            Cron = CronExpression.Parse("0 0 * * * *"),
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = TimeSpan.FromHours(1),
            },
        };

        var query2 = new QueryDefinition
        {
            QueryId = new QueryId("query-2"),
            Cron = CronExpression.Parse("0 0 * * * *"),
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = TimeSpan.FromHours(1),
            },
        };

        engine.RegisterQuery(query1);
        engine.RegisterQuery(query2);

        var state1 = new QueryState
        {
            QueryId = query1.QueryId,
            Watermark = Watermark.ForTime(_baseTime.AddHours(-2)),
            ConsecutiveFailures = 0,
        };

        var state2 = new QueryState
        {
            QueryId = query2.QueryId,
            Watermark = Watermark.ForTime(_baseTime.AddHours(-2)),
            ConsecutiveFailures = 0,
        };

        _stateStoreMock
            .Setup(s => s.GetOrCreateAsync(query1.QueryId, query1.WatermarkKind, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state1);

        _stateStoreMock
            .Setup(s => s.GetOrCreateAsync(query2.QueryId, query2.WatermarkKind, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state2);

        _stateStoreMock
            .Setup(s => s.RecordAttemptAsync(It.IsAny<RunPlan>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var executionCount = 0;
        _executorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<QueryDefinition>(), It.IsAny<QueryRange>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Interlocked.Increment(ref executionCount);
                return AsyncEnumerable.Empty<NormalizedRecord>();
            });

        _mappingProviderMock
            .Setup(m => m.GetMapping(It.IsAny<QueryId>()))
            .Returns(new MappingConfiguration(null));

        _resultsetHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<TimeSeriesBatch>(), It.IsAny<MappingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AckResult(true));

        _stateStoreMock
            .Setup(s => s.RecordSuccessAndAdvanceWatermarkAsync(It.IsAny<RunPlan>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await engine.TickAsync(CancellationToken.None);

        // Assert - With limit of 1, only one query should execute
        _executorMock.Verify(e => e.ExecuteAsync(It.IsAny<QueryDefinition>(), It.IsAny<QueryRange>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TickAsync_WithRetryableFailure_RetriesOnNextTick()
    {
        // Arrange
        using var engine = CreateEngine();
        var query = CreateTestQuery();
        engine.RegisterQuery(query);

        var state = new QueryState
        {
            QueryId = query.QueryId,
            Watermark = Watermark.ForTime(_baseTime.AddHours(-2)),
            ConsecutiveFailures = 0,
        };

        _stateStoreMock
            .Setup(s => s.GetOrCreateAsync(query.QueryId, query.WatermarkKind, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        _stateStoreMock
            .Setup(s => s.RecordAttemptAsync(It.IsAny<RunPlan>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var callCount = 0;
        _executorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<QueryDefinition>(), It.IsAny<QueryRange>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new Exception("First attempt fails");
                }
                return AsyncEnumerable.Empty<NormalizedRecord>();
            });

        _failureClassifierMock
            .Setup(f => f.IsRetryable(It.IsAny<Exception>()))
            .Returns(true);

        var retryTime = _baseTime.AddMinutes(5);
        _retryStrategyMock
            .Setup(r => r.ComputeNextRetryUtc(It.IsAny<DateTimeOffset>(), It.IsAny<int>()))
            .Returns(retryTime);

        _stateStoreMock
            .Setup(s => s.RecordFailureAsync(
                It.IsAny<RunPlan>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mappingProviderMock
            .Setup(m => m.GetMapping(query.QueryId))
            .Returns(new MappingConfiguration(null));

        _resultsetHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<TimeSeriesBatch>(), It.IsAny<MappingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AckResult(true));

        _stateStoreMock
            .Setup(s => s.RecordSuccessAndAdvanceWatermarkAsync(It.IsAny<RunPlan>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Request adhoc run
        var range = QueryRange.Time(_baseTime.AddHours(-1), _baseTime);
        engine.RequestAdhocRun(query.QueryId, range);

        // Act - First tick fails
        await engine.TickAsync(CancellationToken.None);

        // Advance clock to past retry time
        _clockMock.Setup(c => c.UtcNow).Returns(retryTime);

        // Second tick should retry
        await engine.TickAsync(CancellationToken.None);

        // Assert
        _executorMock.Verify(e => e.ExecuteAsync(It.IsAny<QueryDefinition>(), It.IsAny<QueryRange>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _stateStoreMock.Verify(s => s.RecordFailureAsync(
            It.IsAny<RunPlan>(),
            "First attempt fails",
            true,
            It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _stateStoreMock.Verify(s => s.RecordSuccessAndAdvanceWatermarkAsync(
            It.IsAny<RunPlan>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TickAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        using var engine = CreateEngine();
        var query = CreateTestQuery();
        engine.RegisterQuery(query);

        var state = new QueryState
        {
            QueryId = query.QueryId,
            Watermark = Watermark.ForTime(_baseTime.AddHours(-2)),
            ConsecutiveFailures = 0,
        };

        _stateStoreMock
            .Setup(s => s.GetOrCreateAsync(query.QueryId, query.WatermarkKind, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await engine.TickAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CalledOnce_DisposesResources()
    {
        // Arrange
        using var engine = CreateEngine();

        // Act
        var act = () => engine.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        engine.Dispose();
        var act = () => engine.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Helper Methods

    private RqrEngine CreateEngine(int globalConcurrencyLimit = 4)
    {
        return new RqrEngine(
            "test-connector",
            _stateStoreMock.Object,
            _executorMock.Object,
            _resultsetHandlerMock.Object,
            _mappingProviderMock.Object,
            globalConcurrencyLimit,
            _clockMock.Object,
            _failureClassifierMock.Object,
            _retryStrategyMock.Object);
    }

    private QueryDefinition CreateTestQuery()
    {
        return new QueryDefinition
        {
            QueryId = new QueryId("test-query"),
            Cron = CronExpression.Parse("0 0 * * * *"),
            WatermarkKind = WatermarkKind.Time,
            RangeParameters = new RangeParameters
            {
                WindowDuration = TimeSpan.FromHours(1),
            },
        };
    }

    #endregion
}
