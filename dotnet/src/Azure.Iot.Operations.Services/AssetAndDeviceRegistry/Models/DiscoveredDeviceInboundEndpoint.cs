namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DiscoveredDeviceInboundEndpoint
{
    public string? AdditionalConfiguration { get; set; }

    public required string Address { get; set; }

    public required string EndpointType { get; set; }

    public List<string>? SupportedAuthenticationMethods { get; set; }

    public string? Version { get; set; }
}