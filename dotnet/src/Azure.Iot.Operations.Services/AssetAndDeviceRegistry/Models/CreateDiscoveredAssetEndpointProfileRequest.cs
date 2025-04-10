// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record CreateDiscoveredAssetEndpointProfileRequest
{
    public string? AdditionalConfiguration { get; set; } = default;

    public string? Name { get; set; } = default;

    public string? EndpointProfileType { get; set; } = default;

    public List<SupportedAuthenticationMethodsSchemaElement>? SupportedAuthenticationMethods { get; set; } = default;

    public string? TargetAddress { get; set; } = default;
}
