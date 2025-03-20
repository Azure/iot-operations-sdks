/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.CustomTopicTokens
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class CustomTopicTokens
    {
        /// <summary>
        /// Specializes a <c>CommandExecutor</c> class for Command 'readCustomTopicToken'.
        /// </summary>
        public class ReadCustomTopicTokenCommandExecutor : CommandExecutor<EmptyJson, ReadCustomTopicTokenResponsePayload>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ReadCustomTopicTokenCommandExecutor"/> class.
            /// </summary>
            public ReadCustomTopicTokenCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, "readCustomTopicToken", new Utf8JsonSerializer())
            {
                TopicTokenMap["modelId"] = "dtmi:com:example:CustomTopicTokens;1";
                if (mqttClient.ClientId != null)
                {
                    TopicTokenMap["executorId"] = mqttClient.ClientId;
                }
                TopicTokenMap["commandName"] = "readCustomTopicToken";
            }
        }
    }
}
