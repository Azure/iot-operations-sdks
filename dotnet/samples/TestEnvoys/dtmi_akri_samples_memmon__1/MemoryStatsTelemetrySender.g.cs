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
        /// Specializes the <c>TelemetrySender</c> class for type <c>MemoryStatsTelemetry</c>.
        /// </summary>
        public class MemoryStatsTelemetrySender : TelemetrySender<MemoryStatsTelemetry>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MemoryStatsTelemetrySender"/> class.
            /// </summary>
            internal MemoryStatsTelemetrySender(IMqttPubSubClient mqttClient)
                : base(mqttClient, "memoryStats", new AvroSerializer<MemoryStatsTelemetry, EmptyAvro>())
            {
            }
        }
    }
}
