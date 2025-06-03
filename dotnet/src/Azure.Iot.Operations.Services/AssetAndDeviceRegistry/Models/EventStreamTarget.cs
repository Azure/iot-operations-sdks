namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    
    public enum EventStreamTarget
    {
        [EnumMember(Value = @"Mqtt")]
        Mqtt = 0,
        [EnumMember(Value = @"Storage")]
        Storage = 1,
    }
}
