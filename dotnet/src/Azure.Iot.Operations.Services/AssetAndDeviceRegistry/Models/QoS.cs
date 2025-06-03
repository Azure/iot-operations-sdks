namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    
    public enum Qos
    {
        [EnumMember(Value = @"Qos0")]
        Qos0 = 0,
        [EnumMember(Value = @"Qos1")]
        Qos1 = 1,
    }
}
