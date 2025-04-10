    // Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceSpecification
{
        public Dictionary<string, string>? Attributes { get; set; } = default;

        public string? DiscoveredDeviceRef { get; set; } = default;

        public bool? Enabled { get; set; } = default;

        public DeviceEndpoint? Endpoints { get; set; } = default;

        public string? ExternalDeviceId { get; set; } = default;

        public string? LastTransitionTime { get; set; } = default;

        public string? Manufacturer { get; set; } = default;

        public string? Model { get; set; } = default;

        public string? OperatingSystemVersion { get; set; } = default;

        public string? Uuid { get; set; } = default;

        public ulong? Version { get; set; } = default;

}

public record DeviceEndpoint
{
    public Dictionary<string, DeviceInboundEndpointSchemaMapValue>? Inbound { get; set; } = default;
}

public record DeviceInboundEndpointSchemaMapValue
{
    public string? AdditionalConfiguration { get; set; } = default;

    public string Address { get; set; } = default!;

    public Authentication? Authentication { get; set; } = default;

    public TrustSettings? TrustSettings { get; set; } = default;

    public string Type { get; set; } = default!;

    public string? Version { get; set; } = default;
}

public record TrustSettings
{
    public string? IssuerList { get; set; } = default;

    public string? TrustList { get; set; } = default;

    public string TrustMode { get; set; } = default!;
}
