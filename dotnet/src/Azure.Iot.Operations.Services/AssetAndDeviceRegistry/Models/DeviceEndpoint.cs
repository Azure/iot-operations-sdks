// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceEndpoint
{
    public Dictionary<string, InboundEndpointSchemaMapValue>? Inbound { get; set; }

    public OutboundSchema? Outbound { get; set; }
}
