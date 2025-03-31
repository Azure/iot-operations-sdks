/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Math
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class Math
    {
        /// <summary>
        /// Specializes a <c>CommandExecutor</c> class for Command 'fib'.
        /// </summary>
        [CommandBehavior(idempotent: true, cacheTtl: "PT10M")]
        public class FibCommandExecutor : CommandExecutor<FibRequestPayload, FibResponsePayload>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="FibCommandExecutor"/> class.
            /// </summary>
            public FibCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, "fib", new ProtobufSerializer<FibRequestPayload, FibResponsePayload>())
            {
                TopicTokenMap["modelId"] = "dtmi:rpc:samples:math;1";
                if (mqttClient.ClientId != null)
                {
                    TopicTokenMap["executorId"] = mqttClient.ClientId;
                }
                TopicTokenMap["commandName"] = "fib";
            }
        }
    }
}
