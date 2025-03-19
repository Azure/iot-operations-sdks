/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace CloudEvents.Oven
{
    using System;
    using System.Collections.Generic;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using Azure.Iot.Operations.Protocol.Models;
    using CloudEvents;

    public static partial class Oven
    {
        /// <summary>
        /// Specializes the <c>TelemetryReceiver</c> class for type <c>TelemetryCollection</c>.
        /// </summary>
        public class TelemetryReceiver : TelemetryReceiver<TelemetryCollection>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TelemetryReceiver"/> class.
            /// </summary>
            public TelemetryReceiver(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, new Utf8JsonSerializer())
            {
                TopicTokenMap["modelId"] = "dtmi:akri:samples:oven;1";
            }
        }
    }
}
