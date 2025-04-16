namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetStatusConfig
{
    public ConfigError? Error { get; set; }
    
    public string? LastTransitionTime { get; set; }
    
    public ulong? Version { get; set; }
}