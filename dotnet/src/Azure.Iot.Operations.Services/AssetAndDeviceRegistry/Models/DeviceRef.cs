// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceRef
{
    public required string DeviceName { get; set; }

    public required string EndpointName { get; set; }
}
