// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceStatus
{
    public DeviceStatusConfig? Config { get; set; }

    public DeviceStatusEndpoint? Endpoints { get; set; }
}
