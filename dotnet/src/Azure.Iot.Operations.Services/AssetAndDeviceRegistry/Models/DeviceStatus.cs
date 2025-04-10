// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceStatus
{
    public DeviceStatusConfig? Config { get; set; } = default;

    public DeviceStatusEndpoint? Endpoints { get; set; } = default;
}

public record DeviceStatusEndpoint
{
    public Dictionary<string, DeviceStatusInboundEndpointSchemaMapValue>? Inbound { get; set; } = default;
}

public record DeviceStatusInboundEndpointSchemaMapValue
{
    public ConfigError? Error { get; set; } = default;
}

public record DeviceStatusConfig
{
    public ConfigError? Error { get; set; } = default;

    public string? LastTransitionTime { get; set; } = default;

    public ulong? Version { get; set; } = default;
}

public record ConfigError
{
    public string? Code { get; set; } = default;

    public List<DetailsSchemaElement>? Details { get; set; } = default;

    public Dictionary<string, string>? InnerError { get; set; } = default;

    public string? Message { get; set; } = default;
}

public record DetailsSchemaElement
{
    public string? Code { get; set; } = default;

    public string? CorrelationId { get; set; } = default;

    public string? Info { get; set; } = default;

    public string? Message { get; set; } = default;
}
