/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Memmon
{
    using System;
    using System.Collections.Generic;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class Memmon
    {
        /// <summary>
        /// Specializes the <c>TelemetryReceiver</c> class for type <c>WorkingSetTelemetry</c>.
        /// </summary>
        public class WorkingSetTelemetryReceiver : TelemetryReceiver<WorkingSetTelemetry>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="WorkingSetTelemetryReceiver"/> class.
            /// </summary>
            public WorkingSetTelemetryReceiver(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, new AvroSerializer<WorkingSetTelemetry, EmptyAvro>())
            {
                TopicTokenMap["modelId"] = "dtmi:akri:samples:memmon;1";
                TopicTokenMap["telemetryName"] = "workingSet";
            }
        }
    }
}
