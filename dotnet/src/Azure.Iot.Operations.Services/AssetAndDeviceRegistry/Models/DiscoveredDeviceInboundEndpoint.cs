namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class DiscoveredDeviceInboundEndpoint : IJsonOnDeserialized, IJsonOnSerializing
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
        /// The 'endpointType' Field.
        /// </summary>
        [JsonPropertyName("endpointType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string EndpointType { get; set; } = default!;

        /// <summary>
        /// The 'supportedAuthenticationMethods' Field.
        /// </summary>
        [JsonPropertyName("supportedAuthenticationMethods")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<string>? SupportedAuthenticationMethods { get; set; } = default;

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
            if (EndpointType is null)
            {
                throw new ArgumentNullException("endpointType field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Address is null)
            {
                throw new ArgumentNullException("address field cannot be null");
            }
            if (EndpointType is null)
            {
                throw new ArgumentNullException("endpointType field cannot be null");
            }
        }
    }
}
