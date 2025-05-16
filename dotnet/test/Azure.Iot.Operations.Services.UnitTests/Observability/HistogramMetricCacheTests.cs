// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Observability;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.Observability;

public class HistogramMetricCacheTests
{
    [Fact]
    public void CreateHistogram_InitializesHistogram()
    {
        // Arrange
        var cache = new HistogramMetricCache();
        var name = "test_histogram";
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var unit = "milliseconds";

        // Act
        var histogram = cache.CreateHistogram(name, labels, unit);

        // Assert
        Assert.NotNull(histogram);
        Assert.Equal(name, histogram.Name);
        Assert.Equal(labels, histogram.Labels);
        Assert.Equal(unit, histogram.Unit);
    }

    [Fact]
    public void CreateHistogram_SameNameAndLabels_ReturnsSameInstance()
    {
        // Arrange
        var cache = new HistogramMetricCache();
        var name = "test_histogram";
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var unit = "milliseconds";

        // Act
        var histogram1 = cache.CreateHistogram(name, labels, unit);
        var histogram2 = cache.CreateHistogram(name, labels, unit);

        // Assert
        Assert.Same(histogram1, histogram2);
    }

    [Fact]
    public void CreateHistogram_DifferentNames_ReturnsDifferentInstances()
    {
        // Arrange
        var cache = new HistogramMetricCache();
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var unit = "milliseconds";

        // Act
        var histogram1 = cache.CreateHistogram("histogram1", labels, unit);
        var histogram2 = cache.CreateHistogram("histogram2", labels, unit);

        // Assert
        Assert.NotSame(histogram1, histogram2);
    }

    [Fact]
    public void CreateHistogram_SameNameDifferentLabels_ReturnsDifferentInstances()
    {
        // Arrange
        var cache = new HistogramMetricCache();
        var name = "test_histogram";
        var unit = "milliseconds";
        var labels1 = new Dictionary<string, string> { { "service", "test1" } };
        var labels2 = new Dictionary<string, string> { { "service", "test2" } };

        // Act
        var histogram1 = cache.CreateHistogram(name, labels1, unit);
        var histogram2 = cache.CreateHistogram(name, labels2, unit);

        // Assert
        Assert.NotSame(histogram1, histogram2);
    }

    [Fact]
    public void GetMetricsAndClear_NoOperations_ReturnsEmptyList()
    {
        // Arrange
        var cache = new HistogramMetricCache();
        cache.CreateHistogram("histogram", new Dictionary<string, string>(), "milliseconds");

        // Act
        var metrics = cache.GetMetricsAndClear(10);

        // Assert
        Assert.Empty(metrics);
    }

    [Fact]
    public void GetMetricsAndClear_WithOperations_ReturnsMetricsAndClears()
    {
        // Arrange
        var cache = new HistogramMetricCache();
        var name = "test_histogram";
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var unit = "milliseconds";

        var histogram = (IHistogram)cache.CreateHistogram(name, labels, unit);
        histogram.Record(15.2);
        histogram.Record(25.7);

        // Act
        var metrics = cache.GetMetricsAndClear(10);

        // Assert
        Assert.Single(metrics);
        Assert.Equal(name, metrics[0].Definition.Name);
        Assert.Equal(labels, metrics[0].Definition.Labels);
        Assert.Equal(unit, metrics[0].Definition.Unit);
        Assert.Equal(2, metrics[0].Operations.Count);
        Assert.Equal(15.2, metrics[0].Operations[0].Value);
        Assert.Equal(25.7, metrics[0].Operations[1].Value);

        // Verify operations are cleared
        var emptyMetrics = cache.GetMetricsAndClear(10);
        Assert.Empty(emptyMetrics);
    }

    [Fact]
    public void GetMetricsAndClear_MultipleHistograms_ReturnsAllMetrics()
    {
        // Arrange
        var cache = new HistogramMetricCache();

        var histogram1 = (IHistogram)cache.CreateHistogram("histogram1", new Dictionary<string, string> { { "service", "test1" } }, "milliseconds");
        histogram1.Record(15.2);

        var histogram2 = (IHistogram)cache.CreateHistogram("histogram2", new Dictionary<string, string> { { "service", "test2" } }, "seconds");
        histogram2.Record(1.5);

        // Act
        var metrics = cache.GetMetricsAndClear(10);

        // Assert
        Assert.Equal(2, metrics.Count);

        // Check first histogram metrics
        var metric1 = metrics.Find(m => m.Definition.Name == "histogram1");
        Assert.NotNull(metric1);
        Assert.Single(metric1!.Operations);
        Assert.Equal(15.2, metric1.Operations[0].Value);

        // Check second histogram metrics
        var metric2 = metrics.Find(m => m.Definition.Name == "histogram2");
        Assert.NotNull(metric2);
        Assert.Single(metric2!.Operations);
        Assert.Equal(1.5, metric2.Operations[0].Value);
    }

    [Fact]
    public void GetMetricsAndClear_RespectsMaxBatchSize()
    {
        // Arrange
        var cache = new HistogramMetricCache();
        var name = "test_histogram";
        var labels = new Dictionary<string, string> { { "service", "test" } };
        var histogram = (IHistogram)cache.CreateHistogram(name, labels, "milliseconds");

        // Add 5 operations
        for (int i = 10; i <= 14; i++)
        {
            histogram.Record(i);
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
