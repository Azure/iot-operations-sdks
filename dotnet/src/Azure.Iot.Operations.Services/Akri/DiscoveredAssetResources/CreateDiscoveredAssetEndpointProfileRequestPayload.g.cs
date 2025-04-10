/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.DiscoveredAssetResources
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.Akri;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class CreateDiscoveredAssetEndpointProfileRequestPayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command request argument.
        /// </summary>
        [JsonPropertyName("createDiscoveredAssetEndpointProfileRequest")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public  required  CreateDiscoveredAssetEndpointProfileRequestSchema CreateDiscoveredAssetEndpointProfileRequest { get; set; } 

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (CreateDiscoveredAssetEndpointProfileRequest is null)
            {
                throw new ArgumentNullException("createDiscoveredAssetEndpointProfileRequest field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (CreateDiscoveredAssetEndpointProfileRequest is null)
            {
                throw new ArgumentNullException("createDiscoveredAssetEndpointProfileRequest field cannot be null");
            }
        }
    }
}
