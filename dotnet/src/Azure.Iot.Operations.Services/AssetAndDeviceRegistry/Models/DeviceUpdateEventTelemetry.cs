namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class DeviceUpdateEventTelemetry : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'deviceUpdateEvent' Telemetry.
        /// </summary>
        [JsonPropertyName("deviceUpdateEvent")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public DeviceUpdateEvent DeviceUpdateEvent { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (DeviceUpdateEvent is null)
            {
                throw new ArgumentNullException("deviceUpdateEvent field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (DeviceUpdateEvent is null)
            {
                throw new ArgumentNullException("deviceUpdateEvent field cannot be null");
            }
        }
    }
}
