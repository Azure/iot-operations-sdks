namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetDatasetDestinationSchemaElement
{
    public required DestinationConfiguration Configuration { get; set; }
    public DatasetTarget Target { get; set; }
}
