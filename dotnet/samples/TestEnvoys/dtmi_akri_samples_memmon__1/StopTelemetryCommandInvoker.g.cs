/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace TestEnvoys.dtmi_akri_samples_memmon__1
{
    using System;
    using System.Collections.Generic;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class Memmon
    {
        /// <summary>
        /// Specializes the <c>CommandInvoker</c> class for Command 'stopTelemetry'.
        /// </summary>
        public class StopTelemetryCommandInvoker : CommandInvoker<EmptyAvro, EmptyAvro>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="StopTelemetryCommandInvoker"/> class.
            /// </summary>
            internal StopTelemetryCommandInvoker(IMqttPubSubClient mqttClient)
                : base(mqttClient, "stopTelemetry", new AvroSerializer<EmptyAvro, EmptyAvro>())
            {
            }
        }
    }
}
