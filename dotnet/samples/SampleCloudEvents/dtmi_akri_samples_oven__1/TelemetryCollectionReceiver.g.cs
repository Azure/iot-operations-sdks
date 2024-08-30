/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace SampleCloudEvents.dtmi_akri_samples_oven__1
{
    using System;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using MQTTnet.Client;
    using SampleCloudEvents;

    public static partial class Oven
    {
        /// <summary>
        /// Specializes the <c>TelemetryReceiver</c> class for type <c>TelemetryCollection</c>.
        /// </summary>
        public class TelemetryCollectionReceiver : TelemetryReceiver<TelemetryCollection>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TelemetryCollectionReceiver"/> class.
            /// </summary>
            internal TelemetryCollectionReceiver(IMqttPubSubClient mqttClient)
                : base(mqttClient, null, new Utf8JsonSerializer())
            {
            }
        }
    }
}
