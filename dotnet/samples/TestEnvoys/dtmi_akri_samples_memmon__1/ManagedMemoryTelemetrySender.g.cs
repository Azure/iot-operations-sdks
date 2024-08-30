/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace TestEnvoys.dtmi_akri_samples_memmon__1
{
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
            internal ManagedMemoryTelemetrySender(IMqttPubSubClient mqttClient)
                : base(mqttClient, "managedMemory", new AvroSerializer<ManagedMemoryTelemetry, EmptyAvro>())
            {
            }
        }
    }
}
