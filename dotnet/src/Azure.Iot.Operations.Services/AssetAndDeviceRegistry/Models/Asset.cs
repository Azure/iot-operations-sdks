// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record Asset
{
    public required string Name { get; set; }

    public required AssetSpecification Specification { get; set; }

    public AssetStatus? Status { get; set; }
}
