// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEndpointProfile
{
    public string? Name { get; set; } = default;

    public AssetEndpointProfileSpecification? Specification { get; set; } = default;

    public AssetEndpointProfileStatus? Status { get; set; } = default;
}
