// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Observability
{
    public interface IMetric
    {
        string Name { get; }
        Dictionary<string, string> Labels { get; }
        string? Unit { get; }
    }
}
