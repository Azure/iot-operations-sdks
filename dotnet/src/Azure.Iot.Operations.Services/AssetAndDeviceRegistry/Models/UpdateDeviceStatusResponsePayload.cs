namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class UpdateDeviceStatusResponsePayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("updatedDeviceStatus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public DeviceStatus UpdatedDeviceStatus { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (UpdatedDeviceStatus is null)
            {
                throw new ArgumentNullException("updatedDeviceStatus field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (UpdatedDeviceStatus is null)
            {
                throw new ArgumentNullException("updatedDeviceStatus field cannot be null");
            }
        }
    }
}
