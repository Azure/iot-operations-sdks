// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceEndpointsStatus
{
    public Dictionary<string, DeviceStatusInboundEndpointSchemaMapValue>? Inbound { get; set; }
}
