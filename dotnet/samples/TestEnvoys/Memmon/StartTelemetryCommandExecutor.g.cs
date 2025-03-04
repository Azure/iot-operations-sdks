/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Memmon
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class Memmon
    {
        /// <summary>
        /// Specializes a <c>CommandExecutor</c> class for Command 'startTelemetry'.
        /// </summary>
        public class StartTelemetryCommandExecutor : CommandExecutor<StartTelemetryRequestPayload, EmptyAvro>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="StartTelemetryCommandExecutor"/> class.
            /// </summary>
            public StartTelemetryCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, "startTelemetry", new AvroSerializer<StartTelemetryRequestPayload, EmptyAvro>())
            {
                TopicTokenReplacementMap["modelId"] = "dtmi:akri:samples:memmon;1";
                if (mqttClient.ClientId != null)
                {
                    TopicTokenReplacementMap["executorId"] = mqttClient.ClientId;
                }
                TopicTokenReplacementMap["commandName"] = "startTelemetry";
            }
        }
    }
}
