/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace TestEnvoys.dtmi_com_example_Passthrough__1
{
    using System;
    using System.Collections.Generic;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class Passthrough
    {
        /// <summary>
        /// Specializes the <c>CommandInvoker</c> class for Command 'pass'.
        /// </summary>
        public class PassCommandInvoker : CommandInvoker<byte[], byte[]>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PassCommandInvoker"/> class.
            /// </summary>
            internal PassCommandInvoker(IMqttPubSubClient mqttClient)
                : base(mqttClient, "pass", new PassthroughSerializer())
            {
            }
        }
    }
}
