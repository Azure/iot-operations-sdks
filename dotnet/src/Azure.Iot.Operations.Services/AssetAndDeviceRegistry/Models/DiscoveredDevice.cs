namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    public partial class DiscoveredDevice
    {
        /// <summary>
        /// The 'attributes' Field.
        /// </summary>
        [JsonPropertyName("attributes")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Dictionary<string, string>? Attributes { get; set; } = default;

        /// <summary>
        /// The 'endpoints' Field.
        /// </summary>
        [JsonPropertyName("endpoints")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DiscoveredDeviceEndpoints? Endpoints { get; set; } = default;

        /// <summary>
        /// The 'externalDeviceId' Field.
        /// </summary>
        [JsonPropertyName("externalDeviceId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? ExternalDeviceId { get; set; } = default;

        /// <summary>
        /// The 'manufacturer' Field.
        /// </summary>
        [JsonPropertyName("manufacturer")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Manufacturer { get; set; } = default;

        /// <summary>
        /// The 'model' Field.
        /// </summary>
        [JsonPropertyName("model")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Model { get; set; } = default;

        /// <summary>
        /// The 'operatingSystem' Field.
        /// </summary>
        [JsonPropertyName("operatingSystem")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? OperatingSystem { get; set; } = default;

        /// <summary>
        /// The 'operatingSystemVersion' Field.
        /// </summary>
        [JsonPropertyName("operatingSystemVersion")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? OperatingSystemVersion { get; set; } = default;

    }
}
