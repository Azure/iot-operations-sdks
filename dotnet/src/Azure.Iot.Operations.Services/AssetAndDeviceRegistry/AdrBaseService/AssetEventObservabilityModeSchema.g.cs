/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public enum AssetEventObservabilityModeSchema
    {
        [EnumMember(Value = @"Log")]
        Log = 0,
        [EnumMember(Value = @"None")]
        None = 1,
    }
}
