/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace TestEnvoys.dtmi_com_example_Passthrough__1
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class Passthrough
    {
        /// <summary>
        /// Specializes a <c>CommandExecutor</c> class for Command 'pass'.
        /// </summary>
        public class PassCommandExecutor : CommandExecutor<byte[], byte[]>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PassCommandExecutor"/> class.
            /// </summary>
            internal PassCommandExecutor(IMqttPubSubClient mqttClient)
                : base(mqttClient, "pass", new PassthroughSerializer())
            {
            }
        }
    }
}
