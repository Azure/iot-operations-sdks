/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using Azure.Iot.Operations.Protocol.Models;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    public static partial class AdrBaseService
    {
        /// <summary>
        /// Specializes the <c>TelemetrySender</c> class for type <c>DeviceUpdateEventTelemetry</c>.
        /// </summary>
        public class DeviceUpdateEventTelemetrySender : TelemetrySender<DeviceUpdateEventTelemetry>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="DeviceUpdateEventTelemetrySender"/> class.
            /// </summary>
            public DeviceUpdateEventTelemetrySender(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, new Utf8JsonSerializer())
            {
                TopicTokenMap["modelId"] = "dtmi:com:microsoft:akri:AdrBaseService;1";
                if (mqttClient.ClientId != null)
                {
                    TopicTokenMap["senderId"] = mqttClient.ClientId;
                }
                TopicTokenMap["telemetryName"] = "deviceUpdateEvent";
            }
        }
    }
}
