/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1
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
        /// Specializes the <c>CommandInvoker</c> class for Command 'get'.
        /// </summary>
        public class GetCommandInvoker : CommandInvoker<GetCommandRequest, GetCommandResponse>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="GetCommandInvoker"/> class.
            /// </summary>
            internal GetCommandInvoker(IMqttPubSubClient mqttClient)
                : base(mqttClient, "get", new Utf8JsonSerializer())
            {
            }
        }
    }
}
