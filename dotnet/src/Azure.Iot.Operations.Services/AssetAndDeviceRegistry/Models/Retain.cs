namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    
    public enum Retain
    {
        [EnumMember(Value = @"Keep")]
        Keep = 0,
        [EnumMember(Value = @"Never")]
        Never = 1,
    }
}
