/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public enum Format
    {
        [EnumMember(Value = @"Delta/1.0")]
        Delta1 = 0,
        [EnumMember(Value = @"JsonSchema/draft-07")]
        JsonSchemaDraft07 = 1,
    }
}
