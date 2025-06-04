// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record CreateDiscoveredAssetEndpointProfileResponse
{
    public string? DiscoveryId { get; set; }

    public ulong Version { get; set; }
}
