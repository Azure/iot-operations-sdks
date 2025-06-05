namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DiscoveredDeviceEndpoints
{
    public Dictionary<string, DiscoveredDeviceInboundEndpoint>? Inbound { get; set; }

    public DiscoveredDeviceOutboundEndpoints? Outbound { get; set; }
}
