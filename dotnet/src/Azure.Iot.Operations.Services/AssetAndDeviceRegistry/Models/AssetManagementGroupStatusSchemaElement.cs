namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetManagementGroupStatusSchemaElement
{
    public List<AssetManagementGroupActionStatusSchemaElement>? Actions { get; set; }

    public required string Name { get; set; }
}
