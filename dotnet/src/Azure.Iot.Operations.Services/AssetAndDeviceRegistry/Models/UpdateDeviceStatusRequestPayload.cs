namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class UpdateDeviceStatusRequestPayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command request argument.
        /// </summary>
        [JsonPropertyName("deviceStatusUpdate")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public DeviceStatus DeviceStatusUpdate { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (DeviceStatusUpdate is null)
            {
                throw new ArgumentNullException("deviceStatusUpdate field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (DeviceStatusUpdate is null)
            {
                throw new ArgumentNullException("deviceStatusUpdate field cannot be null");
            }
        }
    }
}
