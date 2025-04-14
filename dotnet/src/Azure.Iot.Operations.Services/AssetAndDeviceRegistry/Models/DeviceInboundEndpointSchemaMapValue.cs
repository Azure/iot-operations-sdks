// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceInboundEndpointSchemaMapValue
{
    public string? AdditionalConfiguration { get; set; } = default;

    public string Address { get; set; } = default!;

    public Authentication? Authentication { get; set; } = default;

    public TrustSettings? TrustSettings { get; set; } = default;

    public string Type { get; set; } = default!;

    public string? Version { get; set; } = default;
}
