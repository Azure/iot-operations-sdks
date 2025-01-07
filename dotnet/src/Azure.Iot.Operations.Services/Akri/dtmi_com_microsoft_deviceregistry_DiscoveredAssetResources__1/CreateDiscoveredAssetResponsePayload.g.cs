/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.Akri;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.6.0.0")]
    public class CreateDiscoveredAssetResponsePayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("createDiscoveredAssetResponse")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public Object_CreateDiscoveredAsset_Response CreateDiscoveredAssetResponse { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (CreateDiscoveredAssetResponse is null)
            {
                throw new ArgumentNullException("createDiscoveredAssetResponse field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (CreateDiscoveredAssetResponse is null)
            {
                throw new ArgumentNullException("createDiscoveredAssetResponse field cannot be null");
            }
        }
    }
}
