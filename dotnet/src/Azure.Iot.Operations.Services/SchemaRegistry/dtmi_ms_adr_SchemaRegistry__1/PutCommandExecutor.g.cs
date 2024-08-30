/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1
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
        public class PutCommandExecutor : CommandExecutor<PutCommandRequest, PutCommandResponse>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PutCommandExecutor"/> class.
            /// </summary>
            internal PutCommandExecutor(IMqttPubSubClient mqttClient)
                : base(mqttClient, "put", new Utf8JsonSerializer())
            {
            }
        }
    }
}
