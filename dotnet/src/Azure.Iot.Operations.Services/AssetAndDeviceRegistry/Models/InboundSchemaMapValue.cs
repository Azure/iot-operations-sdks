namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class InboundSchemaMapValue : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// Stringified JSON that contains connectivity type specific further configuration (e.g. OPC UA, Modbus, ONVIF).
        /// </summary>
        [JsonPropertyName("additionalConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? AdditionalConfiguration { get; set; } = default;

        /// <summary>
        /// The endpoint address & port. This can be either an IP address (e.g., 192.168.1.1) or a fully qualified domain name (FQDN, e.g., server.example.com).
        /// </summary>
        [JsonPropertyName("address")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string Address { get; set; } = default!;

        /// <summary>
        /// Definition of the client authentication mechanism to the host.
        /// </summary>
        [JsonPropertyName("authentication")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Authentication? Authentication { get; set; } = default;

        /// <summary>
        /// Type of connection endpoint.
        /// </summary>
        [JsonPropertyName("endpointType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string EndpointType { get; set; } = default!;

        /// <summary>
        /// Trust settings for the endpoint.
        /// </summary>
        [JsonPropertyName("trustSettings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TrustSettings? TrustSettings { get; set; } = default;

        /// <summary>
        /// Version associated with device endpoint.
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
