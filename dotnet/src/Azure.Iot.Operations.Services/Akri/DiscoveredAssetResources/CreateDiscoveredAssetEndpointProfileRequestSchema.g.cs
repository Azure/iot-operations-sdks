/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.DiscoveredAssetResources
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.Akri;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public partial class CreateDiscoveredAssetEndpointProfileRequestSchema
    {
        /// <summary>
        /// A unique identifier for a discovered asset.
        /// </summary>
        [JsonPropertyName("additionalConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? AdditionalConfiguration { get; set; } = default;

        /// <summary>
        /// Name of the discovered asset endpoint profile. If not provided it will get generated by Akri.
        /// </summary>
        [JsonPropertyName("daepName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? DaepName { get; set; } = default;

        /// <summary>
        /// Defines the configuration for the connector type that is being used with the endpoint profile.
        /// </summary>
        [JsonPropertyName("endpointProfileType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? EndpointProfileType { get; set; } = default;

        /// <summary>
        /// list of supported authentication methods
        /// </summary>
        [JsonPropertyName("supportedAuthenticationMethods")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<SupportedAuthenticationMethodsSchemaElementSchema>? SupportedAuthenticationMethods { get; set; } = default;

        /// <summary>
        /// local valid URI specifying the network address/dns name of southbound service.
        /// </summary>
        [JsonPropertyName("targetAddress")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? TargetAddress { get; set; } = default;

    }
}
