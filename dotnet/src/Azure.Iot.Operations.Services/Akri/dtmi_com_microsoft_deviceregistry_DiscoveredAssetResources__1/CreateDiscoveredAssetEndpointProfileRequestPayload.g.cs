/* Code generated by Azure.Iot.Operations.ProtocolCompiler; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.Akri;

    public class CreateDiscoveredAssetEndpointProfileRequestPayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command request argument.
        /// </summary>
        [JsonPropertyName("createDiscoveredAssetEndpointProfileRequest")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public Object_CreateDiscoveredAssetEndpointProfile_Request CreateDiscoveredAssetEndpointProfileRequest { get; set; } = default!;

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
