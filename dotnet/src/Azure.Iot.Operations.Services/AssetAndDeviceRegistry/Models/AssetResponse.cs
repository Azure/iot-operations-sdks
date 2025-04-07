// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetResponse
{
    public string? Name { get; set; } = default;

    public AssetSpecificationSchema? Specification { get; set; } = default;

    public AssetStatus? Status { get; set; } = default;
}
