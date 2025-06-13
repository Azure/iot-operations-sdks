namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    public partial class CreateOrUpdateDiscoveredDeviceRequestPayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command request argument.
        /// </summary>
        [JsonPropertyName("discoveredDeviceRequest")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public CreateOrUpdateDiscoveredDeviceRequestSchema DiscoveredDeviceRequest { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (DiscoveredDeviceRequest is null)
            {
                throw new ArgumentNullException("discoveredDeviceRequest field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (DiscoveredDeviceRequest is null)
            {
                throw new ArgumentNullException("discoveredDeviceRequest field cannot be null");
            }
        }
    }
}
