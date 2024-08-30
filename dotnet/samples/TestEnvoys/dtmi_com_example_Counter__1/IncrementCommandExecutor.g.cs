/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace TestEnvoys.dtmi_com_example_Counter__1
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class Counter
    {
        /// <summary>
        /// Specializes a <c>CommandExecutor</c> class for Command 'increment'.
        /// </summary>
        public class IncrementCommandExecutor : CommandExecutor<EmptyJson, IncrementCommandResponse>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="IncrementCommandExecutor"/> class.
            /// </summary>
            internal IncrementCommandExecutor(IMqttPubSubClient mqttClient)
                : base(mqttClient, "increment", new Utf8JsonSerializer())
            {
            }
        }
    }
}
