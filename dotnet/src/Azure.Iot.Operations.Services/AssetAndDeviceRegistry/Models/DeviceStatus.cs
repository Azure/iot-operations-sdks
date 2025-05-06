// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceStatus
{
    public DeviceConfigStatus? Config { get; set; }

    public DeviceEndpointsStatus? Endpoints { get; set; }
}
