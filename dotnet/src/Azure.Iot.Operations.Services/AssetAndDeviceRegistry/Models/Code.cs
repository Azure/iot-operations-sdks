namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum Code
    {
        [EnumMember(Value = @"BadRequest")]
        BadRequest = 0,
        [EnumMember(Value = @"InternalError")]
        InternalError = 1,
        [EnumMember(Value = @"KubeError")]
        KubeError = 2,
        [EnumMember(Value = @"SerializationError")]
        SerializationError = 3,
    }
}
