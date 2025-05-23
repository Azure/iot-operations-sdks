// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record CreateDiscoveredAssetEndpointProfileResponse
{
    public DiscoveredAssetEndpointProfileResponseStatus Status { get; set; }
}
