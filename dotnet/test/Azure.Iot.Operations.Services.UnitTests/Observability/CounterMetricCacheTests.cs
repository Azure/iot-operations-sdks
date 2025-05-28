// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Observability;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.Observability;

public class CounterMetricCacheTests
{
    [Fact]
    public void CreateCounter_InitializesCounter()
    {
        // Arrange
        var cache = new CounterMetricCache();
        var name = "test_counter";
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var unit = "bytes";

        // Act
        var counter = cache.CreateCounter(name, labels, unit);

        // Assert
        Assert.NotNull(counter);
        Assert.Equal(name, counter.Name);
        Assert.Equal(labels, counter.Labels);
        Assert.Equal(unit, counter.Unit);
    }

    [Fact]
    public void CreateCounter_SameNameAndLabels_ReturnsSameInstance()
    {
        // Arrange
        var cache = new CounterMetricCache();
        var name = "test_counter";
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var unit = "bytes";

        // Act
        var counter1 = cache.CreateCounter(name, labels, unit);
        var counter2 = cache.CreateCounter(name, labels, unit);

        // Assert
        Assert.Same(counter1, counter2);
    }

    [Fact]
    public void CreateCounter_DifferentNames_ReturnsDifferentInstances()
    {
        // Arrange
        var cache = new CounterMetricCache();
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var unit = "bytes";

        // Act
        var counter1 = cache.CreateCounter("counter1", labels, unit);
        var counter2 = cache.CreateCounter("counter2", labels, unit);

        // Assert
        Assert.NotSame(counter1, counter2);
    }

    [Fact]
    public void CreateCounter_SameNameDifferentLabels_ReturnsDifferentInstances()
    {
        // Arrange
        var cache = new CounterMetricCache();
        var name = "test_counter";
        var unit = "bytes";
        var labels1 = new Dictionary<string, string> { { "service", "test1" } };
        var labels2 = new Dictionary<string, string> { { "service", "test2" } };

        // Act
        var counter1 = cache.CreateCounter(name, labels1, unit);
        var counter2 = cache.CreateCounter(name, labels2, unit);

        // Assert
        Assert.NotSame(counter1, counter2);
    }

    [Fact]
    public void GetMetricsAndClear_NoOperations_ReturnsEmptyList()
    {
        // Arrange
        var cache = new CounterMetricCache();
        cache.CreateCounter("counter", new Dictionary<string, string>(), "bytes");

        // Act
        var metrics = cache.GetMetricsAndClear(10);

        // Assert
        Assert.Empty(metrics);
    }

    [Fact]
    public void GetMetricsAndClear_WithOperations_ReturnsMetricsAndClears()
    {
        // Arrange
        var cache = new CounterMetricCache();
        var name = "test_counter";
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var unit = "bytes";

        var counter = (ICounter)cache.CreateCounter(name, labels, unit);
        counter.Add(5);
        counter.Add(10);

        // Act
        var metrics = cache.GetMetricsAndClear(10);

        // Assert
        Assert.Single(metrics);
        Assert.Equal(name, metrics[0].Definition.Name);
        Assert.Equal(labels, metrics[0].Definition.Labels);
        Assert.Equal(unit, metrics[0].Definition.Unit);
        Assert.Equal(2, metrics[0].Operations.Count);
        Assert.Equal(5, metrics[0].Operations[0].Value);
        Assert.Equal(10, metrics[0].Operations[1].Value);

        // Verify operations are cleared
        var emptyMetrics = cache.GetMetricsAndClear(10);
        Assert.Empty(emptyMetrics);
    }

    [Fact]
    public void GetMetricsAndClear_MultipleCounters_ReturnsAllMetrics()
    {
        // Arrange
        var cache = new CounterMetricCache();

        var counter1 = (ICounter)cache.CreateCounter("counter1", new Dictionary<string, string> { { "service", "test1" } }, "bytes");
        counter1.Add(5);

        var counter2 = (ICounter)cache.CreateCounter("counter2", new Dictionary<string, string> { { "service", "test2" } }, "requests");
        counter2.Add(10);

        // Act
        var metrics = cache.GetMetricsAndClear(10);

        // Assert
        Assert.Equal(2, metrics.Count);

        // Check first counter metrics
        var metric1 = metrics.Find(m => m.Definition.Name == "counter1");
        Assert.NotNull(metric1);
        Assert.Single(metric1!.Operations);
        Assert.Equal(5, metric1.Operations[0].Value);

        // Check second counter metrics
        var metric2 = metrics.Find(m => m.Definition.Name == "counter2");
        Assert.NotNull(metric2);
        Assert.Single(metric2!.Operations);
        Assert.Equal(10, metric2.Operations[0].Value);
    }

    [Fact]
    public void GetMetricsAndClear_RespectsMaxBatchSize()
    {
        // Arrange
        var cache = new CounterMetricCache();
        var name = "test_counter";
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var counter = (ICounter)cache.CreateCounter(name, labels, "bytes");

        // Add 5 operations
        for (int i = 1; i <= 5; i++)
        {
            counter.Add(i);
        }

        // Act - Get with max batch size of 3
        var metrics = cache.GetMetricsAndClear(3);

        // Assert
        Assert.Single(metrics);
        Assert.Equal(3, metrics[0].Operations.Count);

        // Get remaining operations
        var remainingMetrics = cache.GetMetricsAndClear(10);
        Assert.Single(remainingMetrics);
        Assert.Equal(2, remainingMetrics[0].Operations.Count);
    }
}
