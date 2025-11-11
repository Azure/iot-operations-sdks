namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetManagementGroupStatus
{
    public List<AssetManagementGroupActionStatus>? Actions { get; set; }

    public required string Name { get; set; }
}
