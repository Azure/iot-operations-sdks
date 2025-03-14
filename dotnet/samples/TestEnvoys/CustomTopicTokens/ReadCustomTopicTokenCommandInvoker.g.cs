/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.CustomTopicTokens
{
    using System;
    using System.Collections.Generic;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class CustomTopicTokens
    {
        /// <summary>
        /// Specializes the <c>CommandInvoker</c> class for Command 'readCustomTopicToken'.
        /// </summary>
        public class ReadCustomTopicTokenCommandInvoker : CommandInvoker<EmptyJson, ReadCustomTopicTokenResponsePayload>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ReadCustomTopicTokenCommandInvoker"/> class.
            /// </summary>
            public ReadCustomTopicTokenCommandInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, "readCustomTopicToken", new Utf8JsonSerializer())
            {
                this.ResponseTopicPrefix = "clients/{invokerClientId}"; // default value, can be overwritten by user code

                TopicTokenMap["modelId"] = "dtmi:com:example:CustomTopicTokens;1";
                if (mqttClient.ClientId != null)
                {
                    TopicTokenMap["invokerClientId"] = mqttClient.ClientId;
                }
                TopicTokenMap["commandName"] = "readCustomTopicToken";
            }
        }
    }
}
