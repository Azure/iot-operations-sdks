// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEndpointProfile
{
    public string? Name { get; set; }

    public AssetEndpointProfileSpecification? Specification { get; set; }

    public AssetEndpointProfileStatus? Status { get; set; }
}
