namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    
    public enum DatasetTarget
    {
        [EnumMember(Value = @"BrokerStateStore")]
        BrokerStateStore = 0,
        [EnumMember(Value = @"Mqtt")]
        Mqtt = 1,
        [EnumMember(Value = @"Storage")]
        Storage = 2,
    }
}
