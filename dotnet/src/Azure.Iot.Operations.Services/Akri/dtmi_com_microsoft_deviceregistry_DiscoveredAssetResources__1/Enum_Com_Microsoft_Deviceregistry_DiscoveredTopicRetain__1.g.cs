/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    /// <summary>
    /// When set to Keep, messages published to MQTT Broker will have the retain flag set.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.6.0.0")]
    public enum Enum_Com_Microsoft_Deviceregistry_DiscoveredTopicRetain__1
    {
        [EnumMember(Value = @"Keep")]
        Keep = 0,
        [EnumMember(Value = @"Never")]
        Never = 1,
    }
}
