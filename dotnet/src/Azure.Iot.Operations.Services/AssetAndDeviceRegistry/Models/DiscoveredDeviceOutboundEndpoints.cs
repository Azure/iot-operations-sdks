namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DiscoveredDeviceOutboundEndpoints
{
    public required Dictionary<string, DeviceOutboundEndpoint> Assigned { get; set; }
}