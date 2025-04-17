// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record CreateDiscoveredAssetEndpointProfileRequest
{
    public string? AdditionalConfiguration { get; set; }

    public string? Name { get; set; }

    public required string EndpointProfileType { get; set; }

    public List<SupportedAuthenticationMethodsSchemaElement>? SupportedAuthenticationMethods { get; set; }

    public required string TargetAddress { get; set; }
}
