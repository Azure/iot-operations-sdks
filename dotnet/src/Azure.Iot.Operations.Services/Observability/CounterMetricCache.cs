// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Azure.Iot.Operations.Services.Observability.AkriObservabilityService;

namespace Azure.Iot.Operations.Services.Observability
{
    internal class CounterMetricCache
    {
        private readonly ConcurrentDictionary<string, CachedCounter> _counters = new();

        public ICounter CreateCounter(string name, Dictionary<string, string> labels, string? unit)
        {
            var key = FormatMetricKey(name, labels);
            return _counters.GetOrAdd(key, _ => new CachedCounter(name, new Dictionary<string, string>(labels), unit));
        }

        public List<CounterMetric> GetMetricsAndClear(int maxBatchSize)
        {
            var result = new List<CounterMetric>();

            foreach (var cachedCounter in _counters.Values)
            {
                var operations = cachedCounter.GetOperationsAndClear(maxBatchSize);
                if (operations.Count > 0)
                {
                    result.Add(new CounterMetric
                    {
                        Definition = new MetricDefinition
                        {
                            Name = cachedCounter.Name,
                            Labels = new Dictionary<string, string>(cachedCounter.Labels),
                            Unit = cachedCounter.Unit
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
}
