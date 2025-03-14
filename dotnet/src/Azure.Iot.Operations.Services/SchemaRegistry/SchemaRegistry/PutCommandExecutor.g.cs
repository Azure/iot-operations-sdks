/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using Azure.Iot.Operations.Services.SchemaRegistry;

    public static partial class SchemaRegistry
    {
        /// <summary>
        /// Specializes a <c>CommandExecutor</c> class for Command 'put'.
        /// </summary>
        public class PutCommandExecutor : CommandExecutor<PutRequestPayload, PutResponsePayload>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PutCommandExecutor"/> class.
            /// </summary>
            public PutCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, "put", new Utf8JsonSerializer())
            {
                TopicTokenMap["modelId"] = "dtmi:ms:adr:SchemaRegistry;1";
                if (mqttClient.ClientId != null)
                {
                    TopicTokenMap["executorId"] = mqttClient.ClientId;
                }
                TopicTokenMap["commandName"] = "put";
            }
        }
    }
}
