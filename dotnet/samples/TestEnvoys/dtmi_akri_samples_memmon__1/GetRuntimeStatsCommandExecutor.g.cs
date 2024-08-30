/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace TestEnvoys.dtmi_akri_samples_memmon__1
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class Memmon
    {
        /// <summary>
        /// Specializes a <c>CommandExecutor</c> class for Command 'getRuntimeStats'.
        /// </summary>
        public class GetRuntimeStatsCommandExecutor : CommandExecutor<GetRuntimeStatsCommandRequest, GetRuntimeStatsCommandResponse>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="GetRuntimeStatsCommandExecutor"/> class.
            /// </summary>
            internal GetRuntimeStatsCommandExecutor(IMqttPubSubClient mqttClient)
                : base(mqttClient, "getRuntimeStats", new AvroSerializer<GetRuntimeStatsCommandRequest, GetRuntimeStatsCommandResponse>())
            {
            }
        }
    }
}
