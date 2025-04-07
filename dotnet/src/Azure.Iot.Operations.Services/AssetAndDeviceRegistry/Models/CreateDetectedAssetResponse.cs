// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record CreateDetectedAssetResponse
{
    public DetectedAssetResponseStatusSchema? Status { get; set; } = default;
}
