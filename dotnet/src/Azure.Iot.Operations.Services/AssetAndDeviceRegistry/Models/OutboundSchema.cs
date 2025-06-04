// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record OutboundSchema
{
    public required Dictionary<string, DeviceOutboundEndpoint> Assigned { get; set; }

    public Dictionary<string, DeviceOutboundEndpoint>? Unassigned { get; set; }
}
