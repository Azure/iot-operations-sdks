/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Memmon
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class Memmon
    {
        /// <summary>
        /// Specializes the <c>TelemetrySender</c> class for type <c>ManagedMemoryTelemetry</c>.
        /// </summary>
        public class ManagedMemoryTelemetrySender : TelemetrySender<ManagedMemoryTelemetry>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ManagedMemoryTelemetrySender"/> class.
            /// </summary>
            public ManagedMemoryTelemetrySender(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, new AvroSerializer<ManagedMemoryTelemetry, EmptyAvro>())
            {
                TopicTokenMap["modelId"] = "dtmi:akri:samples:memmon;1";
                if (mqttClient.ClientId != null)
                {
                    TopicTokenMap["senderId"] = mqttClient.ClientId;
                }
                TopicTokenMap["telemetryName"] = "managedMemory";
            }
        }
    }
}
