namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class DeviceOutboundEndpoint : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'address' Field.
        /// </summary>
        [JsonPropertyName("address")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string Address { get; set; } = default!;

        /// <summary>
        /// The 'endpointType' Field.
        /// </summary>
        [JsonPropertyName("endpointType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? EndpointType { get; set; } = default;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (Address is null)
            {
                throw new ArgumentNullException("address field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Address is null)
            {
                throw new ArgumentNullException("address field cannot be null");
            }
        }
    }
}
