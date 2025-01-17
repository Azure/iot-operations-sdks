/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

#nullable enable

namespace AvroComm.dtmi_codegen_communicationTest_avroModel__1
{
    using System;
    using System.Collections.Generic;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using Azure.Iot.Operations.Protocol.Models;
    using AvroComm;

    public static partial class AvroModel
    {
        /// <summary>
        /// Specializes the <c>TelemetryReceiver</c> class for type <c>TelemetryCollection</c>.
        /// </summary>
        public class TelemetryCollectionReceiver : TelemetryReceiver<TelemetryCollection>
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
            /// Initializes a new instance of the <see cref="TelemetryCollectionReceiver"/> class.
            /// </summary>
            public TelemetryCollectionReceiver(IMqttPubSubClient mqttClient)
                : base(mqttClient, null, new AvroSerializer<TelemetryCollection, EmptyAvro>())
            {
                this.effectiveTopicTokenMap = new(string.Empty, (IReadOnlyDictionary<string, string>)base.TopicTokenMap, "ex:", this.CustomTopicTokenMap);

                base.TopicTokenMap["modelId"] = "dtmi:codegen:communicationTest:avroModel;1";
            }
        }
    }
}
