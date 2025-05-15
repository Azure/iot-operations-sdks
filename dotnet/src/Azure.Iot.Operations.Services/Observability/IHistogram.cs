// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Observability;

public interface IHistogram : IMetric
{
    void Record(double value);
}
