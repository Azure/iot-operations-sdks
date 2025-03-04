/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.StateStore.StateStore
{
    using System;
    using System.Collections.Generic;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using Azure.Iot.Operations.Services.StateStore;

    public static partial class StateStore
    {
        /// <summary>
        /// Specializes the <c>CommandInvoker</c> class for Command 'invoke'.
        /// </summary>
        public class InvokeCommandInvoker : CommandInvoker<byte[], byte[]>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="InvokeCommandInvoker"/> class.
            /// </summary>
            public InvokeCommandInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, "invoke", new PassthroughSerializer())
            {
                this.ResponseTopicPrefix = "clients/{invokerClientId}"; // default value, can be overwritten by user code

                TopicTokenReplacementMap["modelId"] = "dtmi:ms:aio:mq:StateStore;1";
                if (mqttClient.ClientId != null)
                {
                    TopicTokenReplacementMap["invokerClientId"] = mqttClient.ClientId;
                }
                TopicTokenReplacementMap["commandName"] = "invoke";
            }
        }
    }
}
