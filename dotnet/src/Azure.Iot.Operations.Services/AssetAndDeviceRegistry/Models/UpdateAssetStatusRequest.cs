// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record UpdateAssetStatusRequest
{
    public required string AssetName { get; set; }
    public required AssetStatus AssetStatus { get; set; }
}
