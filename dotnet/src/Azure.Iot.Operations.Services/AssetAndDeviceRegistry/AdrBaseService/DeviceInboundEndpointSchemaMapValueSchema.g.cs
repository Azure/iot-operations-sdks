/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class DeviceInboundEndpointSchemaMapValueSchema : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'additionalConfiguration' Field.
        /// </summary>
        [JsonPropertyName("additionalConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? AdditionalConfiguration { get; set; } = default;

        /// <summary>
        /// The 'address' Field.
        /// </summary>
        [JsonPropertyName("address")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string Address { get; set; } = default!;

        /// <summary>
        /// The 'authentication' Field.
        /// </summary>
        [JsonPropertyName("authentication")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AuthenticationSchema? Authentication { get; set; } = default;

        /// <summary>
        /// The 'trustSettings' Field.
        /// </summary>
        [JsonPropertyName("trustSettings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TrustSettingsSchema? TrustSettings { get; set; } = default;

        /// <summary>
        /// The 'type' Field.
        /// </summary>
        [JsonPropertyName("type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string Type { get; set; } = default!;

        /// <summary>
        /// The 'version' Field.
        /// </summary>
        [JsonPropertyName("version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Version { get; set; } = default;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (Address is null)
            {
                throw new ArgumentNullException("address field cannot be null");
            }
            if (Type is null)
            {
                throw new ArgumentNullException("type field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Address is null)
            {
                throw new ArgumentNullException("address field cannot be null");
            }
            if (Type is null)
            {
                throw new ArgumentNullException("type field cannot be null");
            }
        }
    }
}
