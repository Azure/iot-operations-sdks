// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public enum AssetDataPointObservabilityMode
{
    Counter = 0,
    Gauge = 1,
    Histogram = 2,
    Log = 3,
    None = 4
}
