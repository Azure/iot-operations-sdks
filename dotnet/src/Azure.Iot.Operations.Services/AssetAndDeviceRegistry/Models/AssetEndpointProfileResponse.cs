// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEndpointProfileResponse
{
    public string? Name { get; set; } = default;

    public AssetEndpointProfileSpecificationSchema? Specification { get; set; } = default;

    public AssetEndpointProfileStatus? Status { get; set; } = default;
}
