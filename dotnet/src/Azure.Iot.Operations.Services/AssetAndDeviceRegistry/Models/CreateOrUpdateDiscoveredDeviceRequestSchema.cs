namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    public partial class CreateOrUpdateDiscoveredDeviceRequestSchema : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'discoveredDevice' Field.
        /// </summary>
        [JsonPropertyName("discoveredDevice")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public DiscoveredDevice DiscoveredDevice { get; set; } = default!;

        /// <summary>
        /// The 'discoveredDeviceName' Field.
        /// </summary>
        [JsonPropertyName("discoveredDeviceName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string DiscoveredDeviceName { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (DiscoveredDevice is null)
            {
                throw new ArgumentNullException("discoveredDevice field cannot be null");
            }
            if (DiscoveredDeviceName is null)
            {
                throw new ArgumentNullException("discoveredDeviceName field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (DiscoveredDevice is null)
            {
                throw new ArgumentNullException("discoveredDevice field cannot be null");
            }
            if (DiscoveredDeviceName is null)
            {
                throw new ArgumentNullException("discoveredDeviceName field cannot be null");
            }
        }
    }
}
