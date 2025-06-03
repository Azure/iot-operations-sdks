namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    
    public enum Method
    {
        [EnumMember(Value = @"Anonymous")]
        Anonymous = 0,
        [EnumMember(Value = @"Certificate")]
        Certificate = 1,
        [EnumMember(Value = @"UsernamePassword")]
        UsernamePassword = 2,
    }
}
