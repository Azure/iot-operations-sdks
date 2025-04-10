/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class CreateDiscoveredAssetEndpointProfileResponsePayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("createDiscoveredAssetEndpointProfileResponse")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public CreateDiscoveredAssetEndpointProfileResponseSchema CreateDiscoveredAssetEndpointProfileResponse { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (CreateDiscoveredAssetEndpointProfileResponse is null)
            {
                throw new ArgumentNullException("createDiscoveredAssetEndpointProfileResponse field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (CreateDiscoveredAssetEndpointProfileResponse is null)
            {
                throw new ArgumentNullException("createDiscoveredAssetEndpointProfileResponse field cannot be null");
            }
        }
    }
}
