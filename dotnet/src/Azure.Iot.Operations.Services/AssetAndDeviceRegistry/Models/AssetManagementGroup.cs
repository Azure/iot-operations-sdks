using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public class AssetManagementGroup
{
    public List<AssetManagementGroupAction>? Actions { get; set; } = default;

    /// <summary>
    /// Reference to a data source for a given management group.
    /// </summary>
    public string? DataSource { get; set; } = default;

    public ulong? DefaultTimeoutInSeconds { get; set; } = default;

    public string? DefaultTopic { get; set; } = default;

    public string? ManagementGroupConfiguration { get; set; } = default;

    public string Name { get; set; } = default!;

    public string? TypeRef { get; set; } = default;

}
