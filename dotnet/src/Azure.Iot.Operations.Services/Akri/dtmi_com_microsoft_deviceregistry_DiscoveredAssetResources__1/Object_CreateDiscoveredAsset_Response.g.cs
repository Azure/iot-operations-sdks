/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.Akri;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.6.0.0")]
    public partial class Object_CreateDiscoveredAsset_Response
    {
        /// <summary>
        /// status of discovered asset creation
        /// </summary>
        [JsonPropertyName("status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Enum_CreateDiscoveredAsset_Response_Status? Status { get; set; } = default;

    }
}
