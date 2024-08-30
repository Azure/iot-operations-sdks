/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace SampleCloudEvents.dtmi_akri_samples_oven__1
{
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using MQTTnet.Client;
    using SampleCloudEvents;

    public static partial class Oven
    {
        /// <summary>
        /// Specializes the <c>TelemetrySender</c> class for type <c>TelemetryCollection</c>.
        /// </summary>
        public class TelemetryCollectionSender : TelemetrySender<TelemetryCollection>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TelemetryCollectionSender"/> class.
            /// </summary>
            internal TelemetryCollectionSender(IMqttPubSubClient mqttClient)
                : base(mqttClient, null, new Utf8JsonSerializer())
            {
            }
        }
    }
}
