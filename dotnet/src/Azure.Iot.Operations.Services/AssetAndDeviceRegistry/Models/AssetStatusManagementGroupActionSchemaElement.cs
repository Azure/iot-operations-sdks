namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetStatusManagementGroupActionSchemaElement
{
    public ConfigError? Error { get; set; }
    public required string Name { get; set; }
    public MessageSchemaReference? RequestMessageSchemaReference { get; set; }
    public MessageSchemaReference? ResponseMessageSchemaReference { get; set; }
}
