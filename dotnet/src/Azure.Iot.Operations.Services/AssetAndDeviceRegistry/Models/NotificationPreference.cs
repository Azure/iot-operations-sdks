namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    
    public enum NotificationPreference
    {
        [EnumMember(Value = @"Off")]
        Off = 0,
        [EnumMember(Value = @"On")]
        On = 1,
    }
}
