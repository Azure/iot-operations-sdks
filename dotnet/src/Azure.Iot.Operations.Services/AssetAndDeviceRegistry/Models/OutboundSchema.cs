namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class OutboundSchema : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// Device messaging endpoint model.
        /// </summary>
        [JsonPropertyName("assigned")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public Dictionary<string, DeviceOutboundEndpoint> Assigned { get; set; } = default!;

        /// <summary>
        /// Device messaging endpoint model.
        /// </summary>
        [JsonPropertyName("unassigned")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Dictionary<string, DeviceOutboundEndpoint>? Unassigned { get; set; } = default;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (Assigned is null)
            {
                throw new ArgumentNullException("assigned field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Assigned is null)
            {
                throw new ArgumentNullException("assigned field cannot be null");
            }
        }
    }
}
