/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.6.0.0")]
    public enum Enum_Ms_Adr_SchemaRegistry_Format__1
    {
        [EnumMember(Value = @"Delta/1.0")]
        Delta1 = 0,
        [EnumMember(Value = @"JsonSchema/draft-07")]
        JsonSchemaDraft07 = 1,
    }
}
