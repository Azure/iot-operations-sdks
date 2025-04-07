// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEndpointProfileSpecification
{
    public string? AdditionalConfiguration { get; set; } = default;

    public Authentication? Authentication { get; set; } = default;

    public string? DiscoveredAssetEndpointProfileRef { get; set; } = default;

    public string? EndpointProfileType { get; set; } = default;

    public string? TargetAddress { get; set; } = default;

    public string? Uuid { get; set; } = default;
}
