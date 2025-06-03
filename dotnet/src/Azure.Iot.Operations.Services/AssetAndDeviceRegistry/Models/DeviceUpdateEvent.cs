namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class DeviceUpdateEvent : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'device' Field.
        /// </summary>
        [JsonPropertyName("device")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public Device Device { get; set; } = default!;

        /// <summary>
        /// The 'deviceName' Field.
        /// </summary>
        [JsonPropertyName("deviceName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string DeviceName { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (Device is null)
            {
                throw new ArgumentNullException("device field cannot be null");
            }
            if (DeviceName is null)
            {
                throw new ArgumentNullException("deviceName field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Device is null)
            {
                throw new ArgumentNullException("device field cannot be null");
            }
            if (DeviceName is null)
            {
                throw new ArgumentNullException("deviceName field cannot be null");
            }
        }
    }
}
