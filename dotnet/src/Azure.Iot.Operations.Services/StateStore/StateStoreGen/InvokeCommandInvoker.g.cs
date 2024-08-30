/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace Azure.Iot.Operations.Services.StateStore.dtmi_ms_aio_mq_StateStore__1
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
            internal InvokeCommandInvoker(IMqttPubSubClient mqttClient)
                : base(mqttClient, "invoke", new PassthroughSerializer())
            {
            }
        }
    }
}
