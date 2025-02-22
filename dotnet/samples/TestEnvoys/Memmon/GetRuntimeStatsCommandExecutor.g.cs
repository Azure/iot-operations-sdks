/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Memmon
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
        public class GetRuntimeStatsCommandExecutor : CommandExecutor<GetRuntimeStatsRequestPayload, GetRuntimeStatsResponsePayload>
        {
            private CombinedPrefixedReadOnlyDictionary<string> effectiveTopicTokenMap;

            /// <summary>
            /// Optionally initializes a custom token map to a dictionary that maps token values to replacement strings; defaults to new empty dictionary.
            /// </summary>
            public Dictionary<string, string> CustomTopicTokenMap { private get; init; } = new();

            /// <summary>
            /// Gets a dictionary for adding custom token keys and their replacement strings, which will be substituted in request and response topic patterns.
            /// Note that keys will automatically be prefixed by "ex:" when used for substitution searches in topic pattern strings.
            /// </summary>
            public override Dictionary<string, string> TopicTokenMap { get => CustomTopicTokenMap; }

            /// <summary>
            /// Gets a dictionary used by the base class's code for substituting tokens in request and response topic patterns.
            /// </summary>
            protected override IReadOnlyDictionary<string, string> EffectiveTopicTokenMap { get => effectiveTopicTokenMap; }

            /// <summary>
            /// Initializes a new instance of the <see cref="GetRuntimeStatsCommandExecutor"/> class.
            /// </summary>
            public GetRuntimeStatsCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, "getRuntimeStats", new AvroSerializer<GetRuntimeStatsRequestPayload, GetRuntimeStatsResponsePayload>())
            {
                this.effectiveTopicTokenMap = new(string.Empty, (IReadOnlyDictionary<string, string>)base.TopicTokenMap, "ex:", this.CustomTopicTokenMap);

                base.TopicTokenMap["modelId"] = "dtmi:akri:samples:memmon;1";
                if (mqttClient.ClientId != null)
                {
                    base.TopicTokenMap["executorId"] = mqttClient.ClientId;
                }
                base.TopicTokenMap["commandName"] = "getRuntimeStats";
            }
        }
    }
}
