/* Code generated by Azure.Iot.Operations.ProtocolCompiler; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.Akri;

    public class Object_CreateDiscoveredAsset_Request_Events_ElementSchema_Topic
    {
        /// <summary>
        /// The 'path' Field.
        /// </summary>
        [JsonPropertyName("path")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Path { get; set; } = default;

        /// <summary>
        /// The 'retain' Field.
        /// </summary>
        [JsonPropertyName("retain")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Enum_Com_Microsoft_Deviceregistry_DiscoveredTopicRetain__1? Retain { get; set; } = default;

    }
}
