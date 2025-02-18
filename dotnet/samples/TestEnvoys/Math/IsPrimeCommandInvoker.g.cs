/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.8.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Math
{
    using System;
    using System.Collections.Generic;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class Math
    {
        /// <summary>
        /// Specializes the <c>CommandInvoker</c> class for Command 'isPrime'.
        /// </summary>
        public class IsPrimeCommandInvoker : CommandInvoker<IsPrimeRequestPayload, IsPrimeResponsePayload>
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
            /// Initializes a new instance of the <see cref="IsPrimeCommandInvoker"/> class.
            /// </summary>
            public IsPrimeCommandInvoker(IMqttPubSubClient mqttClient)
                : base(mqttClient, "isPrime", new ProtobufSerializer<IsPrimeRequestPayload, IsPrimeResponsePayload>())
            {
                this.ResponseTopicPrefix = "clients/{invokerClientId}"; // default value, can be overwritten by user code

                this.effectiveTopicTokenMap = new(string.Empty, (IReadOnlyDictionary<string, string>)base.TopicTokenMap, "ex:", this.CustomTopicTokenMap);

                base.TopicTokenMap["modelId"] = "dtmi:rpc:samples:math;1";
                base.TopicTokenMap["invokerClientId"] = mqttClient.ClientId!;
                base.TopicTokenMap["commandName"] = "isPrime";
            }
        }
    }
}
