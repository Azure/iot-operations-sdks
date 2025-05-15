// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Azure.Iot.Operations.Services.Observability.AkriObservabilityService;

namespace Azure.Iot.Operations.Services.Observability;

internal class HistogramMetricCache
{
    private readonly ConcurrentDictionary<string, CachedHistogram> _histograms = new();

    public IHistogram CreateHistogram(string name, Dictionary<string, string> labels, string? unit)
    {
        var key = FormatMetricKey(name, labels);
        return _histograms.GetOrAdd(key, _ => new CachedHistogram(name, new Dictionary<string, string>(labels), unit));
    }

    public List<HistogramMetric> GetMetricsAndClear(int maxBatchSize)
    {
        var result = new List<HistogramMetric>();

        foreach (var cachedHistogram in _histograms.Values)
        {
            var operations = cachedHistogram.GetOperationsAndClear(maxBatchSize);
            if (operations.Count > 0)
            {
                result.Add(new HistogramMetric
                {
                    Definition = new MetricDefinition
                    {
                        Name = cachedHistogram.Name,
                        Labels = new Dictionary<string, string>(cachedHistogram.Labels),
                        Unit = cachedHistogram.Unit
                    },
                    Operations = operations
                });
            }
        }

        return result;
    }

    private static string FormatMetricKey(string name, Dictionary<string, string> labels)
    {
        var labelString = string.Join(",", labels.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return $"{name}:{labelString}";
    }
}
