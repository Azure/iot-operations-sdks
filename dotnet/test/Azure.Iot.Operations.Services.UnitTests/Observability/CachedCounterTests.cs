// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Observability;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.Observability;

public class CachedCounterTests
{
    private readonly string _name = "test_counter";

    private readonly Dictionary<string, string> _labels = new()
    {
        { "service", "test" },
        { "instance", "instance1" }
    };

    private readonly string _unit = "bytes";

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Arrange & Act
        var counter = new CachedCounter(_name, _labels, _unit);

        // Assert
        Assert.Equal(_name, counter.Name);
        Assert.Equal(_labels, counter.Labels);
        Assert.Equal(_unit, counter.Unit);
    }

    [Fact]
    public void Add_AddsOperationToQueue()
    {
        // Arrange
        var counter = new CachedCounter(_name, _labels, _unit);
        var value = 5.0;

        // Act
        counter.Add(value);

        // Assert
        var operations = counter.GetOperationsAndClear(10);
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
        var counter = new CachedCounter(_name, _labels, _unit);

        // Act
        counter.Add(3.0);
        counter.Add(1.0);
        counter.Add(7.5);

        // Assert
        var operations = counter.GetOperationsAndClear(10);
        Assert.Equal(3, operations.Count);
        Assert.Equal(3.0, operations[0].Value);
        Assert.Equal(1.0, operations[1].Value);
        Assert.Equal(7.5, operations[2].Value);
    }

    [Fact]
    public void GetOperationsAndClear_RespectsMaxCount()
    {
        // Arrange
        var counter = new CachedCounter(_name, _labels, _unit);

        // Add 5 operations
        for (var i = 1; i <= 5; i++) counter.Add(i);

        // Act - Get only 3 operations
        var firstBatch = counter.GetOperationsAndClear(3);

        // Assert
        Assert.Equal(3, firstBatch.Count);
        Assert.Equal(1.0, firstBatch[0].Value);
        Assert.Equal(2.0, firstBatch[1].Value);
        Assert.Equal(3.0, firstBatch[2].Value);

        // Get remaining operations
        var secondBatch = counter.GetOperationsAndClear(10);
        Assert.Equal(2, secondBatch.Count);
        Assert.Equal(4.0, secondBatch[0].Value);
        Assert.Equal(5.0, secondBatch[1].Value);
    }

    [Fact]
    public void GetOperationsAndClear_EmptiesQueue()
    {
        // Arrange
        var counter = new CachedCounter(_name, _labels, _unit);
        counter.Add(1.0);
        counter.Add(2.0);

        // Act
        var operations = counter.GetOperationsAndClear(10);

        // Assert
        Assert.Equal(2, operations.Count);

        // Verify queue is now empty
        var emptyOperations = counter.GetOperationsAndClear(10);
        Assert.Empty(emptyOperations);
    }

    [Fact]
    public void GetOperationsAndClear_WithEmptyQueue_ReturnsEmptyList()
    {
        // Arrange
        var counter = new CachedCounter(_name, _labels, _unit);

        // Act
        var operations = counter.GetOperationsAndClear(10);

        // Assert
        Assert.Empty(operations);
    }
}
