// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record CreateOrUpdateDiscoveredAssetRequest
{
    public required DiscoveredAsset DiscoveredAsset { get; set; }

    public required string DiscoveredAssetName { get; set; }
}
