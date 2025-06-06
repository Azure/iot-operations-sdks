    // Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceSpecification
{
        public Dictionary<string, string>? Attributes { get; set; }

        public string? DiscoveredDeviceRef { get; set; }

        public bool? Enabled { get; set; }

        public DeviceEndpoint? Endpoints { get; set; }

        public string? ExternalDeviceId { get; set; }

        public DateTime? LastTransitionTime { get; set; }

        public string? Manufacturer { get; set; }

        public string? Model { get; set; }

        public string? OperatingSystemVersion { get; set; }

        public string? Uuid { get; set; }

        public ulong? Version { get; set; }

}
