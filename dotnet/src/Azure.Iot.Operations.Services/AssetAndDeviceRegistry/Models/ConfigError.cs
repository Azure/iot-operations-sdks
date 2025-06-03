namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class ConfigError
    {
        /// <summary>
        /// Error code for classification of errors (ex: '400', '404', '500', etc.).
        /// </summary>
        [JsonPropertyName("code")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Code { get; set; } = default;

        /// <summary>
        /// Array of event statuses that describe the status of each event.
        /// </summary>
        [JsonPropertyName("details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<DetailsSchemaElement>? Details { get; set; } = default;

        /// <summary>
        /// A set of key-value pairs that contain service specific details set by the service.
        /// </summary>
        [JsonPropertyName("innerError")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Dictionary<string, string>? InnerError { get; set; } = default;

        /// <summary>
        /// Human readable helpful error message to provide additional context for error (ex: “capability Id ''foo'' does not exist”).
        /// </summary>
        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Message { get; set; } = default;

    }
}
