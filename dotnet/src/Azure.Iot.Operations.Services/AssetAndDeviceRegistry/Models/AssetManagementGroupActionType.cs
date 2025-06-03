namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    
    public enum AssetManagementGroupActionType
    {
        [EnumMember(Value = @"Call")]
        Call = 0,
        [EnumMember(Value = @"Read")]
        Read = 1,
        [EnumMember(Value = @"Write")]
        Write = 2,
    }
}
