namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    public partial class AkriServiceError : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'code' Field.
        /// </summary>
        [JsonPropertyName("code")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public Code Code { get; set; } = default!;

        /// <summary>
        /// The 'message' Field.
        /// </summary>
        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string Message { get; set; } = default!;

        /// <summary>
        /// The 'timestamp' Field.
        /// </summary>
        [JsonPropertyName("timestamp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public DateTime Timestamp { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (Message is null)
            {
                throw new ArgumentNullException("message field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Message is null)
            {
                throw new ArgumentNullException("message field cannot be null");
            }
        }
    }
}
