/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace TestEnvoys.dtmi_akri_samples_memmon__1
{
    using System;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class Memmon
    {
        /// <summary>
        /// Specializes the <c>TelemetryReceiver</c> class for type <c>ManagedMemoryTelemetry</c>.
        /// </summary>
        public class ManagedMemoryTelemetryReceiver : TelemetryReceiver<ManagedMemoryTelemetry>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ManagedMemoryTelemetryReceiver"/> class.
            /// </summary>
            internal ManagedMemoryTelemetryReceiver(IMqttPubSubClient mqttClient)
                : base(mqttClient, "managedMemory", new AvroSerializer<ManagedMemoryTelemetry, EmptyAvro>())
            {
            }
        }
    }
}
