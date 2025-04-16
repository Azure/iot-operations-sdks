/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class AssetStatusManagementGroupActionSchemaElementSchema : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'error' Field.
        /// </summary>
        [JsonPropertyName("error")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public ConfigError? Error { get; set; } = default;

        /// <summary>
        /// The 'name' Field.
        /// </summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string Name { get; set; } = default!;

        /// <summary>
        /// The 'requestMessageSchemaReference' Field.
        /// </summary>
        [JsonPropertyName("requestMessageSchemaReference")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public MessageSchemaReference? RequestMessageSchemaReference { get; set; } = default;

        /// <summary>
        /// The 'responseMessageSchemaReference' Field.
        /// </summary>
        [JsonPropertyName("responseMessageSchemaReference")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public MessageSchemaReference? ResponseMessageSchemaReference { get; set; } = default;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (Name is null)
            {
                throw new ArgumentNullException("name field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Name is null)
            {
                throw new ArgumentNullException("name field cannot be null");
            }
        }
    }
}
