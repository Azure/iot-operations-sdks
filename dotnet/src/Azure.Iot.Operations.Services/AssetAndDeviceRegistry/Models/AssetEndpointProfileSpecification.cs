// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEndpointProfileSpecification
{
    public string? AdditionalConfiguration { get; set; }

    public Authentication? Authentication { get; set; }

    public string? DiscoveredAssetEndpointProfileRef { get; set; }

    public string? EndpointProfileType { get; set; }

    public string? TargetAddress { get; set; }

    public string? Uuid { get; set; }
}
