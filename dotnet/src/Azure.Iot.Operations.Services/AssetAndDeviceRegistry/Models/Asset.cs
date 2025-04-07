// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record Asset
{
    public string? Name { get; set; } = default;

    public AssetSpecification? Specification { get; set; } = default;

    public AssetStatus? Status { get; set; } = default;
}
