namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DatasetDestination
{
    public required DestinationConfiguration Configuration { get; set; }
    public DatasetTarget Target { get; set; }
}
