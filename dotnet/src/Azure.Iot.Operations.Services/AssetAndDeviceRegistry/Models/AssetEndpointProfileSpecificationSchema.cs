// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEndpointProfileSpecificationSchema
{
    public string? AdditionalConfiguration { get; set; } = default;

    public AuthenticationSchema? Authentication { get; set; } = default;

    public string? DiscoveredAssetEndpointProfileRef { get; set; } = default;

    public string? EndpointProfileType { get; set; } = default;

    public string? TargetAddress { get; set; } = default;

    public string? Uuid { get; set; } = default;
}
