// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record CreateDiscoveredDeviceRequest
{
    public required string Name { get; set; }

    public Dictionary<string, string>? Attributes { get; set; }

    public DiscoveredDeviceEndpoint? Endpoints { get; set; }

    public string? ExternalDeviceId { get; set; }

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public string? OperatingSystem { get; set; }

    public string? OperatingSystemVersion { get; set; }
}
