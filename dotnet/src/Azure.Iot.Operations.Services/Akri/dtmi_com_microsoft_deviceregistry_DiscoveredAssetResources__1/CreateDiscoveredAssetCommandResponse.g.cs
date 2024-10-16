/* Code generated by Azure.Iot.Operations.ProtocolCompiler; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class CreateDiscoveredAssetCommandResponse : IJsonOnDeserialized, IJsonOnSerializing
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
