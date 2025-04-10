/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class MessageSchemaReference : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'schemaName' Field.
        /// </summary>
        [JsonPropertyName("schemaName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string SchemaName { get; set; } = default!;

        /// <summary>
        /// The 'schemaRegistryNamespace' Field.
        /// </summary>
        [JsonPropertyName("schemaRegistryNamespace")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string SchemaRegistryNamespace { get; set; } = default!;

        /// <summary>
        /// The 'schemaVersion' Field.
        /// </summary>
        [JsonPropertyName("schemaVersion")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string SchemaVersion { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (SchemaName is null)
            {
                throw new ArgumentNullException("schemaName field cannot be null");
            }
            if (SchemaRegistryNamespace is null)
            {
                throw new ArgumentNullException("schemaRegistryNamespace field cannot be null");
            }
            if (SchemaVersion is null)
            {
                throw new ArgumentNullException("schemaVersion field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (SchemaName is null)
            {
                throw new ArgumentNullException("schemaName field cannot be null");
            }
            if (SchemaRegistryNamespace is null)
            {
                throw new ArgumentNullException("schemaRegistryNamespace field cannot be null");
            }
            if (SchemaVersion is null)
            {
                throw new ArgumentNullException("schemaVersion field cannot be null");
            }
        }
    }
}
