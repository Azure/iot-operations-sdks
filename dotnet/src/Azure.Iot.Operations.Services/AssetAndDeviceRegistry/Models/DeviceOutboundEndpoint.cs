namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceOutboundEndpoint
{
    public required string Address { get; set; }

    public string? EndpointType { get; set; }
}
