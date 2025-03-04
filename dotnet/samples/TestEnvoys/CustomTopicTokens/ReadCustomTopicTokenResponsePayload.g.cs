/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.CustomTopicTokens
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using TestEnvoys;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public partial class ReadCustomTopicTokenResponsePayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("CustomTopicTokenResponse")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string CustomTopicTokenResponse { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (CustomTopicTokenResponse is null)
            {
                throw new ArgumentNullException("CustomTopicTokenResponse field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (CustomTopicTokenResponse is null)
            {
                throw new ArgumentNullException("CustomTopicTokenResponse field cannot be null");
            }
        }
    }
}
