/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.DiscoveredAssetResources
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public enum StatusSchema
    {
        [EnumMember(Value = @"created")]
        Created = 0,
        [EnumMember(Value = @"duplicate")]
        Duplicate = 1,
        [EnumMember(Value = @"failed")]
        Failed = 2,
    }
}
