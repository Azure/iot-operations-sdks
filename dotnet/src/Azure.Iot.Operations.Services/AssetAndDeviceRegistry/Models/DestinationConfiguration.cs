namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DestinationConfiguration
{
    public string? Key { get; set; }
    public string? Path { get; set; }
    public QoS? Qos { get; set; }
    public Retain? Retain { get; set; }
    public string? Topic { get; set; }
    public ulong? Ttl { get; set; }
}