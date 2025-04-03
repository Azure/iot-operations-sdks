// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEndpointProfile
{
    public string? Name { get; set; } = default;

    public AssetEndpointProfileSpecificationSchema? Specification { get; set; } = default;

    public AssetEndpointProfileStatus? Status { get; set; } = default;

}

public record AssetEndpointProfileSpecificationSchema
{
    public string? AdditionalConfiguration { get; set; } = default;

    public AuthenticationSchema? Authentication { get; set; } = default;

    public string? DiscoveredAssetEndpointProfileRef { get; set; } = default;

    public string? EndpointProfileType { get; set; } = default;

    public string? TargetAddress { get; set; } = default;

    public string? Uuid { get; set; } = default;

}

public record AssetEndpointProfileStatus
{
    public List<Error>? Errors { get; set; } = default;
}
