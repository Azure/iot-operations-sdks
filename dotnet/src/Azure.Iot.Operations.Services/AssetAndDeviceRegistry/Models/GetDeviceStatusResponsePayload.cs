namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class GetDeviceStatusResponsePayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("deviceStatus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public DeviceStatus DeviceStatus { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (DeviceStatus is null)
            {
                throw new ArgumentNullException("deviceStatus field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (DeviceStatus is null)
            {
                throw new ArgumentNullException("deviceStatus field cannot be null");
            }
        }
    }
}
