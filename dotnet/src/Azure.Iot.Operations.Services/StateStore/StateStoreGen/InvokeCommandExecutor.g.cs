/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace Azure.Iot.Operations.Services.StateStore.dtmi_ms_aio_mq_StateStore__1
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using Azure.Iot.Operations.Services.StateStore;

    public static partial class StateStore
    {
        /// <summary>
        /// Specializes a <c>CommandExecutor</c> class for Command 'invoke'.
        /// </summary>
        public class InvokeCommandExecutor : CommandExecutor<byte[], byte[]>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="InvokeCommandExecutor"/> class.
            /// </summary>
            internal InvokeCommandExecutor(IMqttPubSubClient mqttClient)
                : base(mqttClient, "invoke", new PassthroughSerializer())
            {
            }
        }
    }
}
