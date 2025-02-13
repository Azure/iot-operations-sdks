/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.8.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.DiscoveredAssetResources
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    /// <summary>
    /// When set to Keep, messages published to MQTT Broker will have the retain flag set.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.8.0.0")]
    public enum DiscoveredTopicRetain
    {
        [EnumMember(Value = @"Keep")]
        Keep = 0,
        [EnumMember(Value = @"Never")]
        Never = 1,
    }
}
