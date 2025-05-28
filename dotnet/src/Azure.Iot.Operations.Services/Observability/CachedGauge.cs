// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Azure.Iot.Operations.Services.Observability.AkriObservabilityService;

namespace Azure.Iot.Operations.Services.Observability;

internal class CachedGauge : IGauge
{
    private readonly ConcurrentQueue<RecordOperation> _operations = new();

    public string Name { get; }
    public Dictionary<string, string> Labels { get; }
    public string? Unit { get; }

    public CachedGauge(string name, Dictionary<string, string> labels, string? unit)
    {
        Name = name;
        Labels = labels;
        Unit = unit;
    }

    public void Record(double value)
    {
        _operations.Enqueue(new RecordOperation
        {
            OperationId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Value = value
        });
    }

    public List<RecordOperation> GetOperationsAndClear(int maxCount)
    {
        var result = new List<RecordOperation>();

        int count = 0;
        while (count < maxCount && _operations.TryDequeue(out var operation))
        {
            result.Add(operation);
            count++;
        }

        return result;
    }
}
