# RqrEngine Unit Tests

This document describes the comprehensive unit tests created for the `RqrEngine` class.

## Test Project Structure

**Project:** `tests/ResilientQueryRunner.Tests/ResilientQueryRunner.Tests.csproj`

**Frameworks:**
- xUnit for test execution
- Moq for mocking dependencies
- FluentAssertions for readable assertions

## Test Files

### 1. RqrEngineTests.cs
Main unit tests covering all public methods of `RqrEngine`.

#### Constructor Tests
- ? `Constructor_WithValidParameters_CreatesInstance` - Validates successful instantiation
- ? `Constructor_WithInvalidConnectorIdentity_ThrowsArgumentException` - Tests null/empty/whitespace connector identity
- ? `Constructor_WithInvalidConcurrencyLimit_ThrowsArgumentOutOfRangeException` - Tests zero/negative limits
- ? `Constructor_WithNullOptionalParameters_UsesDefaults` - Verifies default implementations are used

#### RegisterQuery Tests
- ? `RegisterQuery_WithValidQuery_RegistersSuccessfully` - Normal registration flow
- ? `RegisterQuery_WithInvalidRangeParameters_ThrowsException` - Validates range parameter validation
- ? `RegisterQuery_RegistersSameQueryTwice_OverwritesFirst` - Tests idempotency

#### RequestAdhocRun Tests
- ? `RequestAdhocRun_WithValidParameters_EnqueuesRun` - Normal adhoc run request
- ? `RequestAdhocRun_WithPriorityOverride_UsesPriorityOverride` - Tests priority override
- ? `RequestAdhocRun_WithoutPriorityOverride_UsesMaxIntPriority` - Verifies default priority

#### TickAsync Tests (Core Engine Loop)
- ? `TickAsync_WithNoQueries_CompletesSuccessfully` - Empty engine tick
- ? `TickAsync_WithRegisteredQuery_PlansAndExecutesRuns` - Full execution workflow
- ? `TickAsync_WithQueryInRetryWindow_SkipsExecution` - Respects retry delays
- ? `TickAsync_WithNoRangesToExecute_DoesNotExecute` - Caught-up queries don't execute
- ? `TickAsync_WithAdhocRun_ExecutesAdhocRun` - Adhoc runs are processed
- ? `TickAsync_WithFailedExecution_RecordsFailure` - Failure handling
- ? `TickAsync_WithNonRetryableFailure_RecordsFailureWithoutRetry` - Non-retryable failures
- ? `TickAsync_WithResultsetHandlerFailure_RecordsFailure` - Handler failures
- ? `TickAsync_WithGlobalConcurrencyLimit_RespectsLimit` - Concurrency enforcement
- ? `TickAsync_WithRetryableFailure_RetriesOnNextTick` - Retry mechanism
- ? `TickAsync_WithCancellation_ThrowsOperationCanceledException` - Cancellation support

#### Dispose Tests
- ? `Dispose_CalledOnce_DisposesResources` - Clean disposal
- ? `Dispose_CalledMultipleTimes_DoesNotThrow` - Idempotent disposal

### 2. RqrEngineIntegrationTests.cs
End-to-end integration tests using real implementations.

#### Integration Scenarios
- ? `FullWorkflow_WithTimeBasedQuery_ExecutesSuccessfully` - Complete workflow with InMemoryStateStore
- ? `CatchUp_WithMultipleWindows_ExecutesInOrder` - Multi-window catch-up processing
- ? `AdhocRun_WithHighPriority_ExecutesBeforeScheduled` - Priority ordering verification
- ? `FailedRun_WithRetry_EventuallySucceeds` - Retry until success scenario
- ? `MultipleQueries_WithConcurrencyLimit_ExecutesConcurrently` - Concurrent query execution

**Test Helpers:**
- `TestClock` - Controllable clock implementation
- `TestQueryExecutor` - Simple executor returning test data
- `FailingTestQueryExecutor` - Executor that fails N times then succeeds
- `TestResultsetHandler` - Records handled batches
- `TestMappingProvider` - Simple mapping provider

### 3. RqrEngineEdgeCaseTests.cs
Edge cases and boundary condition tests.

#### Edge Cases
- ? `TickAsync_WithEmptyResultset_AdvancesWatermark` - Empty results should advance watermark
- ? `TickAsync_WithLargeNumberOfRecords_ProcessesAll` - 10,000 record processing
- ? `TickAsync_WithMultipleSeriesIds_GroupsCorrectly` - Series grouping and ordering
- ? `TickAsync_WithAdhocRunNotAdvancingWatermark_DoesNotAdvanceWatermark` - Adhoc watermark control
- ? `TickAsync_WithOverlappingRanges_HandlesCorrectly` - Range overlap functionality
- ? `TickAsync_WithConsecutiveFailures_IncrementsFailureCount` - Failure tracking
- ? `TickAsync_WithOperationCanceledException_DoesNotRecordFailure` - Cancellation doesn't count as failure
- ? `TickAsync_WithQualityValues_IncludesQualityInBatch` - Optional quality field handling

## Test Coverage Summary

### Methods Tested
1. **Constructor** - 4 tests covering all parameter validations
2. **RegisterQuery** - 3 tests covering registration scenarios
3. **RequestAdhocRun** - 3 tests covering adhoc execution requests
4. **TickAsync** - 19 tests covering the main execution loop
5. **Dispose** - 2 tests for resource cleanup

### Private Method Coverage
While private methods aren't directly tested, they are thoroughly exercised through the public API:
- `TryDequeueBest` - Tested through priority ordering scenarios
- `ExecuteRunAsync` - Tested through all tick scenarios
- `EnqueueRetry` - Tested through retry scenarios
- `ReleaseDueRetries` - Tested through retry timing scenarios

### Code Coverage Areas
- ? Query registration and validation
- ? Scheduled run planning
- ? Adhoc run execution
- ? Priority queue management
- ? Concurrency control
- ? Watermark advancement
- ? Failure classification
- ? Retry scheduling
- ? Resultset building
- ? Empty resultset handling
- ? Large dataset handling
- ? Multi-series grouping
- ? Quality value handling
- ? Range overlap
- ? Cancellation handling
- ? Resource disposal

## Running the Tests

```bash
# Run all tests
dotnet test tests/ResilientQueryRunner.Tests/ResilientQueryRunner.Tests.csproj

# Run with detailed output
dotnet test tests/ResilientQueryRunner.Tests/ResilientQueryRunner.Tests.csproj --verbosity detailed

# Run specific test class
dotnet test --filter "FullyQualifiedName~RqrEngineTests"

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Test Patterns Used

### 1. Arrange-Act-Assert (AAA)
All tests follow the AAA pattern for clarity:
```csharp
// Arrange
using var engine = CreateEngine();
// ... setup

// Act
var result = await engine.TickAsync(CancellationToken.None);

// Assert
result.Should().BeTrue();
```

### 2. Mock Verification
Uses Moq to verify interactions:
```csharp
_stateStoreMock.Verify(s => s.RecordSuccessAndAdvanceWatermarkAsync(
    It.IsAny<RunPlan>(),
    It.IsAny<DateTimeOffset>(),
    It.IsAny<CancellationToken>()), Times.Once);
```

### 3. Fluent Assertions
Readable assertions:
```csharp
capturedBatch.Should().NotBeNull();
capturedBatch!.Series.Should().HaveCount(3);
capturedBatch.Series.Should().BeInAscendingOrder(s => s.Id);
```

### 4. Theory Tests
Data-driven tests for multiple scenarios:
```csharp
[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
public void Constructor_WithInvalidConnectorIdentity_ThrowsArgumentException(string? connectorIdentity)
```

## Mocking Strategy

### Core Dependencies
- `IStateStore` - Mocked to control state behavior
- `IHostQueryExecutor` - Mocked to simulate query execution
- `IHostResultsetHandler` - Mocked to capture resultsets
- `IMappingConfigurationProvider` - Mocked for mapping config
- `IClock` - Mocked for time control
- `IFailureClassifier` - Mocked for failure classification
- `IRetryStrategy` - Mocked for retry timing

### Integration Tests
Use real implementations:
- `InMemoryStateStore` - Real state storage
- Custom test implementations for executors/handlers

## Test Maintenance

### Adding New Tests
1. Identify the scenario to test
2. Choose appropriate test class (unit/integration/edge case)
3. Follow AAA pattern
4. Use descriptive test names: `MethodName_Scenario_ExpectedBehavior`
5. Add helper methods to reduce duplication

### Common Helper Methods
- `CreateEngine()` - Creates engine with mocked dependencies
- `CreateTestQuery()` - Creates standard test query definition
- `GenerateLargeRecordSet()` - Creates large test datasets
- `GenerateMultiSeriesRecords()` - Creates multi-series test data

## Notes

1. Tests are designed to be deterministic and not depend on timing
2. All async operations are properly awaited
3. Resources are properly disposed using `using` statements
4. Tests are isolated and can run in any order
5. Mock setups are explicit and verifiable

## Future Enhancements

Potential additions:
- Performance benchmarks
- Stress tests for concurrency
- More complex cron schedule scenarios
- Index-based watermark tests (when implemented)
- SLA miss tracking tests
- Metrics collection verification
- Distributed tracing verification
