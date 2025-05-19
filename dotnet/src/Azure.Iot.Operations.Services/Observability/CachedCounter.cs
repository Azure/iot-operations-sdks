// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Azure.Iot.Operations.Services.Observability.AkriObservabilityService;

namespace Azure.Iot.Operations.Services.Observability;

internal class CachedCounter : ICounter
{
    private readonly ConcurrentQueue<IncrementOperation> _operations = new();

    public string Name { get; }
    public Dictionary<string, string> Labels { get; }
    public string? Unit { get; }

    public CachedCounter(string name, Dictionary<string, string> labels, string? unit)
    {
        Name = name;
        Labels = labels;
        Unit = unit;
    }

    public void Add(double value)
    {
        _operations.Enqueue(new IncrementOperation
        {
            OperationId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Value = value
        });
    }

    public List<IncrementOperation> GetOperationsAndClear(int maxCount)
    {
        var result = new List<IncrementOperation>();

        int count = 0;
        while (count < maxCount && _operations.TryDequeue(out var operation))
        {
            result.Add(operation);
            count++;
        }

        return result;
    }
}
