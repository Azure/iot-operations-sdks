// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record CreateDetectedAssetResponse
{
    public required string DiscoveryId { get; set; }

    public required ulong Version { get; set; }
}
