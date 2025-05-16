// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Observability;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.Observability;

public class CachedGaugeTests
{
    private readonly string _name = "test_gauge";

    private readonly Dictionary<string, string> _labels = new()
    {
        { "service", "test" },
        { "instance", "instance1" }
    };

    private readonly string _unit = "celsius";

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Arrange & Act
        var gauge = new CachedGauge(_name, _labels, _unit);

        // Assert
        Assert.Equal(_name, gauge.Name);
        Assert.Equal(_labels, gauge.Labels);
        Assert.Equal(_unit, gauge.Unit);
    }

    [Fact]
    public void Record_AddsOperationToQueue()
    {
        // Arrange
        var gauge = new CachedGauge(_name, _labels, _unit);
        var value = 23.5;

        // Act
        gauge.Record(value);

        // Assert
        var operations = gauge.GetOperationsAndClear(10);
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
        var gauge = new CachedGauge(_name, _labels, _unit);

        // Act
        gauge.Record(25.0);
        gauge.Record(26.5);
        gauge.Record(24.8);

        // Assert
        var operations = gauge.GetOperationsAndClear(10);
        Assert.Equal(3, operations.Count);
        Assert.Equal(25.0, operations[0].Value);
        Assert.Equal(26.5, operations[1].Value);
        Assert.Equal(24.8, operations[2].Value);
    }

    [Fact]
    public void GetOperationsAndClear_RespectsMaxCount()
    {
        // Arrange
        var gauge = new CachedGauge(_name, _labels, _unit);

        // Add 5 operations
        for (var i = 20; i < 25; i++) gauge.Record(i);

        // Act - Get only 3 operations
        var firstBatch = gauge.GetOperationsAndClear(3);

        // Assert
        Assert.Equal(3, firstBatch.Count);
        Assert.Equal(20.0, firstBatch[0].Value);
        Assert.Equal(21.0, firstBatch[1].Value);
        Assert.Equal(22.0, firstBatch[2].Value);

        // Get remaining operations
        var secondBatch = gauge.GetOperationsAndClear(10);
        Assert.Equal(2, secondBatch.Count);
        Assert.Equal(23.0, secondBatch[0].Value);
        Assert.Equal(24.0, secondBatch[1].Value);
    }

    [Fact]
    public void GetOperationsAndClear_EmptiesQueue()
    {
        // Arrange
        var gauge = new CachedGauge(_name, _labels, _unit);
        gauge.Record(22.0);
        gauge.Record(23.0);

        // Act
        var operations = gauge.GetOperationsAndClear(10);

        // Assert
        Assert.Equal(2, operations.Count);

        // Verify queue is now empty
        var emptyOperations = gauge.GetOperationsAndClear(10);
        Assert.Empty(emptyOperations);
    }

    [Fact]
    public void GetOperationsAndClear_WithEmptyQueue_ReturnsEmptyList()
    {
        // Arrange
        var gauge = new CachedGauge(_name, _labels, _unit);

        // Act
        var operations = gauge.GetOperationsAndClear(10);

        // Assert
        Assert.Empty(operations);
    }
}
