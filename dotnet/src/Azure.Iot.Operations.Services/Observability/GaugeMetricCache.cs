// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Azure.Iot.Operations.Services.Observability.AkriObservabilityService;

namespace Azure.Iot.Operations.Services.Observability;

internal class GaugeMetricCache
{
    private readonly ConcurrentDictionary<string, CachedGauge> _gauges = new();

    public IGauge CreateGauge(string name, Dictionary<string, string> labels, string? unit)
    {
        var key = FormatMetricKey(name, labels);
        return _gauges.GetOrAdd(key, _ => new CachedGauge(name, new Dictionary<string, string>(labels), unit));
    }

    public List<GaugeMetric> GetMetricsAndClear(int maxBatchSize)
    {
        var result = new List<GaugeMetric>();

        foreach (var cachedGauge in _gauges.Values)
        {
            List<RecordOperation> operations = cachedGauge.GetOperationsAndClear(maxBatchSize);
            if (operations.Count > 0)
            {
                result.Add(new GaugeMetric
                {
                    Definition = new MetricDefinition
                    {
                        Name = cachedGauge.Name,
                        Labels = new Dictionary<string, string>(cachedGauge.Labels),
                        Unit = cachedGauge.Unit
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
