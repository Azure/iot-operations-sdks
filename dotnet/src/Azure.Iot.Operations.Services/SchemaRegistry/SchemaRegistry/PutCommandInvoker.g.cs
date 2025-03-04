/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry
{
    using System;
    using System.Collections.Generic;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using Azure.Iot.Operations.Services.SchemaRegistry;

    public static partial class SchemaRegistry
    {
        /// <summary>
        /// Specializes the <c>CommandInvoker</c> class for Command 'put'.
        /// </summary>
        public class PutCommandInvoker : CommandInvoker<PutRequestPayload, PutResponsePayload>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PutCommandInvoker"/> class.
            /// </summary>
            public PutCommandInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, "put", new Utf8JsonSerializer())
            {
                this.ResponseTopicPrefix = "clients/{invokerClientId}"; // default value, can be overwritten by user code

                TopicTokenReplacementMap["modelId"] = "dtmi:ms:adr:SchemaRegistry;1";
                if (mqttClient.ClientId != null)
                {
                    TopicTokenReplacementMap["invokerClientId"] = mqttClient.ClientId;
                }
                TopicTokenReplacementMap["commandName"] = "put";
            }
        }
    }
}
