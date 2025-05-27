using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public class AssetManagementGroup
{
    public List<AssetManagementGroupAction>? Actions { get; set; } = default;

    public uint? DefaultTimeOutInSeconds { get; set; } = default;

    public string? DefaultTopic { get; set; } = default;

    public JsonDocument? ManagementGroupConfiguration { get; set; } = default;

    public string Name { get; set; } = default!;

    public string? TypeRef { get; set; } = default;

}