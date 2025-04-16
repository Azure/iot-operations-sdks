using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEventDataPointSchemaElement
{
    public JsonDocument? DataPointConfiguration { get; set; }
    public required string DataSource { get; set; }
    public required string Name { get; set; }
}
