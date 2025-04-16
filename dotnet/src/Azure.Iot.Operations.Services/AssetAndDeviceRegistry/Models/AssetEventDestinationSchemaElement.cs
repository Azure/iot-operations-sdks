namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEventDestinationSchemaElement
{
    public required DestinationConfiguration Configuration { get; set; }
    public EventStreamTarget Target { get; set; }
}
