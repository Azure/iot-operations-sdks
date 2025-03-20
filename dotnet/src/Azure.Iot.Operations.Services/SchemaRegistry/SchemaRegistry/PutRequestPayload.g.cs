/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.SchemaRegistry;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class PutRequestPayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command request argument.
        /// </summary>
        [JsonPropertyName("putSchemaRequest")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public PutRequestSchema PutSchemaRequest { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (PutSchemaRequest is null)
            {
                throw new ArgumentNullException("putSchemaRequest field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (PutSchemaRequest is null)
            {
                throw new ArgumentNullException("putSchemaRequest field cannot be null");
            }
        }
    }
}
