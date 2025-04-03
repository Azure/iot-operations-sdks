// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record CreateDiscoveredAssetEndpointProfileRequest
{
    public string? AdditionalConfiguration { get; set; } = default;

    public string? DaepName { get; set; } = default;

    public string? EndpointProfileType { get; set; } = default;

    public List<SupportedAuthenticationMethodsSchemaElementSchema>? SupportedAuthenticationMethods { get; set; } = default;

    public string? TargetAddress { get; set; } = default;
}
