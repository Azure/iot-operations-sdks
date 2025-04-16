namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetStatusManagementGroupSchemaElement
{
    public List<AssetStatusManagementGroupActionSchemaElement>? Actions { get; set; }

    public required string Name { get; set; }
}
