/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Memmon
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class Memmon
    {
        /// <summary>
        /// Specializes the <c>TelemetrySender</c> class for type <c>ManagedMemoryTelemetry</c>.
        /// </summary>
        public class ManagedMemoryTelemetrySender : TelemetrySender<ManagedMemoryTelemetry>
        {
            private CombinedPrefixedReadOnlyDictionary<string> effectiveTopicTokenMap;

            /// <summary>
            /// Optionally initializes a custom token map to a dictionary that maps token values to replacement strings; defaults to new empty dictionary.
            /// </summary>
            public Dictionary<string, string> CustomTopicTokenMap { private get; init; } = new();

            /// <summary>
            /// Gets a dictionary for adding custom token keys and their replacement strings, which will be substituted in telemetry topic patterns.
            /// Note that keys will automatically be prefixed by "ex:" when used for substitution searches in topic pattern strings.
            /// </summary>
            public override Dictionary<string, string> TopicTokenMap { get => CustomTopicTokenMap; }

            /// <summary>
            /// Gets a dictionary used by the base class's code for substituting tokens in telemetry topic patterns.
            /// </summary>
            protected override IReadOnlyDictionary<string, string> EffectiveTopicTokenMap { get => effectiveTopicTokenMap; }

            /// <summary>
            /// Initializes a new instance of the <see cref="ManagedMemoryTelemetrySender"/> class.
            /// </summary>
            public ManagedMemoryTelemetrySender(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, new AvroSerializer<ManagedMemoryTelemetry, EmptyAvro>())
            {
                this.effectiveTopicTokenMap = new(string.Empty, (IReadOnlyDictionary<string, string>)base.TopicTokenMap, "ex:", this.CustomTopicTokenMap);

                base.TopicTokenMap["modelId"] = "dtmi:akri:samples:memmon;1";
                if (mqttClient.ClientId != null)
                {
                    base.TopicTokenMap["senderId"] = mqttClient.ClientId;
                }
                else
                {
                    base.TopicTokenMap["senderId"] = Guid.NewGuid().ToString();
                }
                base.TopicTokenMap["telemetryName"] = "managedMemory";
            }
        }
    }
}
