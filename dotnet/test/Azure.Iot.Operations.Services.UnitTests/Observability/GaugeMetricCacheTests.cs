// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Observability;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.Observability;

public class GaugeMetricCacheTests
{
    [Fact]
    public void CreateGauge_InitializesGauge()
    {
        // Arrange
        var cache = new GaugeMetricCache();
        var name = "test_gauge";
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var unit = "celsius";

        // Act
        var gauge = cache.CreateGauge(name, labels, unit);

        // Assert
        Assert.NotNull(gauge);
        Assert.Equal(name, gauge.Name);
        Assert.Equal(labels, gauge.Labels);
        Assert.Equal(unit, gauge.Unit);
    }

    [Fact]
    public void CreateGauge_SameNameAndLabels_ReturnsSameInstance()
    {
        // Arrange
        var cache = new GaugeMetricCache();
        var name = "test_gauge";
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var unit = "celsius";

        // Act
        var gauge1 = cache.CreateGauge(name, labels, unit);
        var gauge2 = cache.CreateGauge(name, labels, unit);

        // Assert
        Assert.Same(gauge1, gauge2);
    }

    [Fact]
    public void CreateGauge_DifferentNames_ReturnsDifferentInstances()
    {
        // Arrange
        var cache = new GaugeMetricCache();
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var unit = "celsius";

        // Act
        var gauge1 = cache.CreateGauge("gauge1", labels, unit);
        var gauge2 = cache.CreateGauge("gauge2", labels, unit);

        // Assert
        Assert.NotSame(gauge1, gauge2);
    }

    [Fact]
    public void CreateGauge_SameNameDifferentLabels_ReturnsDifferentInstances()
    {
        // Arrange
        var cache = new GaugeMetricCache();
        var name = "test_gauge";
        var unit = "celsius";
        var labels1 = new Dictionary<string, string> { { "service", "test1" } };
        var labels2 = new Dictionary<string, string> { { "service", "test2" } };

        // Act
        var gauge1 = cache.CreateGauge(name, labels1, unit);
        var gauge2 = cache.CreateGauge(name, labels2, unit);

        // Assert
        Assert.NotSame(gauge1, gauge2);
    }

    [Fact]
    public void GetMetricsAndClear_NoOperations_ReturnsEmptyList()
    {
        // Arrange
        var cache = new GaugeMetricCache();
        cache.CreateGauge("gauge", new Dictionary<string, string>(), "celsius");

        // Act
        var metrics = cache.GetMetricsAndClear(10);

        // Assert
        Assert.Empty(metrics);
    }

    [Fact]
    public void GetMetricsAndClear_WithOperations_ReturnsMetricsAndClears()
    {
        // Arrange
        var cache = new GaugeMetricCache();
        var name = "test_gauge";
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var unit = "celsius";

        var gauge = (IGauge)cache.CreateGauge(name, labels, unit);
        gauge.Record(22.5);
        gauge.Record(23.8);

        // Act
        var metrics = cache.GetMetricsAndClear(10);

        // Assert
        Assert.Single(metrics);
        Assert.Equal(name, metrics[0].Definition.Name);
        Assert.Equal(labels, metrics[0].Definition.Labels);
        Assert.Equal(unit, metrics[0].Definition.Unit);
        Assert.Equal(2, metrics[0].Operations.Count);
        Assert.Equal(22.5, metrics[0].Operations[0].Value);
        Assert.Equal(23.8, metrics[0].Operations[1].Value);

        // Verify operations are cleared
        var emptyMetrics = cache.GetMetricsAndClear(10);
        Assert.Empty(emptyMetrics);
    }

    [Fact]
    public void GetMetricsAndClear_MultipleGauges_ReturnsAllMetrics()
    {
        // Arrange
        var cache = new GaugeMetricCache();

        var gauge1 = (IGauge)cache.CreateGauge("gauge1", new Dictionary<string, string> { { "service", "test1" } }, "celsius");
        gauge1.Record(22.5);

        var gauge2 = (IGauge)cache.CreateGauge("gauge2", new Dictionary<string, string> { { "service", "test2" } }, "fahrenheit");
        gauge2.Record(72.0);

        // Act
        var metrics = cache.GetMetricsAndClear(10);

        // Assert
        Assert.Equal(2, metrics.Count);

        // Check first gauge metrics
        var metric1 = metrics.Find(m => m.Definition.Name == "gauge1");
        Assert.NotNull(metric1);
        Assert.Single(metric1!.Operations);
        Assert.Equal(22.5, metric1.Operations[0].Value);

        // Check second gauge metrics
        var metric2 = metrics.Find(m => m.Definition.Name == "gauge2");
        Assert.NotNull(metric2);
        Assert.Single(metric2!.Operations);
        Assert.Equal(72.0, metric2.Operations[0].Value);
    }

    [Fact]
    public void GetMetricsAndClear_RespectsMaxBatchSize()
    {
        // Arrange
        var cache = new GaugeMetricCache();
        var name = "test_gauge";
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var gauge = (IGauge)cache.CreateGauge(name, labels, "celsius");

        // Add 5 operations
        for (int i = 20; i <= 24; i++)
        {
            gauge.Record(i);
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
