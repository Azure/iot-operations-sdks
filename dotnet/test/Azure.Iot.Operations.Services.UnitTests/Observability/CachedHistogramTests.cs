// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Observability;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.Observability;

public class CachedHistogramTests
{
    private readonly string _name = "test_histogram";

    private readonly Dictionary<string, string> _labels = new()
    {
        { "service", "test" },
        { "instance", "instance1" }
    };

    private readonly string _unit = "milliseconds";

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Arrange & Act
        var histogram = new CachedHistogram(_name, _labels, _unit);

        // Assert
        Assert.Equal(_name, histogram.Name);
        Assert.Equal(_labels, histogram.Labels);
        Assert.Equal(_unit, histogram.Unit);
    }

    [Fact]
    public void Record_AddsOperationToQueue()
    {
        // Arrange
        var histogram = new CachedHistogram(_name, _labels, _unit);
        var value = 15.7;

        // Act
        histogram.Record(value);

        // Assert
        var operations = histogram.GetOperationsAndClear(10);
        Assert.Single(operations);
        Assert.Equal(value, operations[0].Value);
        Assert.NotNull(operations[0].OperationId);
        Assert.True(operations[0].Timestamp <= DateTime.UtcNow);
        Assert.True(operations[0].Timestamp >= DateTime.UtcNow.AddMinutes(-1)); // Should be recent
    }

    [Fact]
    public void MultipleOperations_AddedCorrectly()
    {
        // Arrange
        var histogram = new CachedHistogram(_name, _labels, _unit);

        // Act
        histogram.Record(12.5);
        histogram.Record(18.7);
        histogram.Record(21.3);

        // Assert
        var operations = histogram.GetOperationsAndClear(10);
        Assert.Equal(3, operations.Count);
        Assert.Equal(12.5, operations[0].Value);
        Assert.Equal(18.7, operations[1].Value);
        Assert.Equal(21.3, operations[2].Value);
    }

    [Fact]
    public void GetOperationsAndClear_RespectsMaxCount()
    {
        // Arrange
        var histogram = new CachedHistogram(_name, _labels, _unit);

        // Add 5 operations
        for (var i = 10; i < 15; i++) histogram.Record(i);

        // Act - Get only 3 operations
        var firstBatch = histogram.GetOperationsAndClear(3);

        // Assert
        Assert.Equal(3, firstBatch.Count);
        Assert.Equal(10.0, firstBatch[0].Value);
        Assert.Equal(11.0, firstBatch[1].Value);
        Assert.Equal(12.0, firstBatch[2].Value);

        // Get remaining operations
        var secondBatch = histogram.GetOperationsAndClear(10);
        Assert.Equal(2, secondBatch.Count);
        Assert.Equal(13.0, secondBatch[0].Value);
        Assert.Equal(14.0, secondBatch[1].Value);
    }

    [Fact]
    public void GetOperationsAndClear_EmptiesQueue()
    {
        // Arrange
        var histogram = new CachedHistogram(_name, _labels, _unit);
        histogram.Record(12.5);
        histogram.Record(18.7);

        // Act
        var operations = histogram.GetOperationsAndClear(10);

        // Assert
        Assert.Equal(2, operations.Count);

        // Verify queue is now empty
        var emptyOperations = histogram.GetOperationsAndClear(10);
        Assert.Empty(emptyOperations);
    }

    [Fact]
    public void GetOperationsAndClear_WithEmptyQueue_ReturnsEmptyList()
    {
        // Arrange
        var histogram = new CachedHistogram(_name, _labels, _unit);

        // Act
        var operations = histogram.GetOperationsAndClear(10);

        // Assert
        Assert.Empty(operations);
    }

    [Fact]
    public async Task Concurrent_Operations_ThreadSafe()
    {
        // Arrange
        var histogram = new CachedHistogram(_name, _labels, _unit);
        var operationCount = 1000;
        var threads = 5;

        // Act
        var tasks = new List<Task>();
        for (int t = 0; t < threads; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < operationCount / threads; i++)
                {
                    histogram.Record(i);
                }
            }));
        }

        await Task.WhenAll(tasks.ToArray());

        // Assert
        var operations = histogram.GetOperationsAndClear(operationCount);
        Assert.Equal(operationCount, operations.Count);
    }
}
