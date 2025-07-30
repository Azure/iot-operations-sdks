// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record InboundEndpointSchemaMapValue
{
    public string? AdditionalConfiguration { get; set; }

    public string Address { get; set; } = default!;

    public Authentication? Authentication { get; set; }

    public TrustSettings? TrustSettings { get; set; }

    public string EndpointType { get; set; } = default!;

    public string? Version { get; set; }
}
