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
        /// Specializes a <c>CommandExecutor</c> class for Command 'readCounter'.
        /// </summary>
        public class ReadCounterCommandExecutor : CommandExecutor<EmptyJson, ReadCounterCommandResponse>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ReadCounterCommandExecutor"/> class.
            /// </summary>
            internal ReadCounterCommandExecutor(IMqttPubSubClient mqttClient)
                : base(mqttClient, "readCounter", new Utf8JsonSerializer())
            {
            }
        }
    }
}
