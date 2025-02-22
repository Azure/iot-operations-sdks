/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.SchemaRegistry;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public partial class PutResponsePayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("schema")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public Schema Schema { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (Schema is null)
            {
                throw new ArgumentNullException("schema field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Schema is null)
            {
                throw new ArgumentNullException("schema field cannot be null");
            }
        }
    }
}
