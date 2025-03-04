/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Counter
{
    using System;
    using System.Collections.Generic;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class Counter
    {
        /// <summary>
        /// Specializes the <c>CommandInvoker</c> class for Command 'increment'.
        /// </summary>
        public class IncrementCommandInvoker : CommandInvoker<IncrementRequestPayload, IncrementResponsePayload>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="IncrementCommandInvoker"/> class.
            /// </summary>
            public IncrementCommandInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, "increment", new Utf8JsonSerializer())
            {
                this.ResponseTopicPrefix = "clients/{invokerClientId}"; // default value, can be overwritten by user code

                TopicTokenReplacementMap["modelId"] = "dtmi:com:example:Counter;1";
                if (mqttClient.ClientId != null)
                {
                    TopicTokenReplacementMap["invokerClientId"] = mqttClient.ClientId;
                }
                TopicTokenReplacementMap["commandName"] = "increment";
            }
        }
    }
}
