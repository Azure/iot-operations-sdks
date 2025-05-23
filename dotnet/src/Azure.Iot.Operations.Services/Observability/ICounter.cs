// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Observability;

public interface ICounter : IMetric
{
    void Add(double value);
}
