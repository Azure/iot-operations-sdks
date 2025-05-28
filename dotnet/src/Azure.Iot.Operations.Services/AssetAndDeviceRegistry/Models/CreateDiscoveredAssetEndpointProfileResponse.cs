// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record CreateDiscoveredAssetEndpointProfileResponse
{
    public string? Name { get; set; }

    public SpecSchema? Spec { get; set; }
}

public record SpecSchema
{
    public Dictionary<string, string>? Attributes { get; set; }

    public required string DiscoveryId { get; set; }

    public DiscoveredDeviceEndpoint? Endpoints { get; set; }

    public string? ExternalDeviceId { get; set; }

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public string? OperatingSystem { get; set; }

    public string? OperatingSystemVersion { get; set; }

    public ulong Version { get; set; }
}
