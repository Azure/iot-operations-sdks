namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetStatusStreamSchemaElement
{
    public ConfigError? Error { get; set; }
    public MessageSchemaReference? MessageSchemaReference { get; set; }
    public required string Name { get; set; }
}
