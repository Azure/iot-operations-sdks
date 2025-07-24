using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public class AssetManagementGroupAction
{
    public string? ActionConfiguration { get; set; } = default;

    public AssetManagementGroupActionType ActionType { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string TargetUri { get; set; } = default!;

    public uint? TimeOutInSeconds { get; set; } = default;

    public string? Topic { get; set; } = default;

    public string? TypeRef { get; set; } = default;
}
