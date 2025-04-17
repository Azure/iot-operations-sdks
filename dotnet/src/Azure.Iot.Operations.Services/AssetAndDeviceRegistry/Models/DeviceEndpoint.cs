// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceEndpoint
{
    public Dictionary<string, DeviceInboundEndpointSchemaMapValue>? Inbound { get; set; }
}
