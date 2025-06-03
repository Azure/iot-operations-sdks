namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class DiscoveredAssetStream : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'destinations' Field.
        /// </summary>
        [JsonPropertyName("destinations")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<EventStreamDestination>? Destinations { get; set; } = default;

        /// <summary>
        /// The 'lastUpdatedOn' Field.
        /// </summary>
        [JsonPropertyName("lastUpdatedOn")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime? LastUpdatedOn { get; set; } = default;

        /// <summary>
        /// The 'name' Field.
        /// </summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string Name { get; set; } = default!;

        /// <summary>
        /// The 'streamConfiguration' Field.
        /// </summary>
        [JsonPropertyName("streamConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? StreamConfiguration { get; set; } = default;

        /// <summary>
        /// The 'typeRef' Field.
        /// </summary>
        [JsonPropertyName("typeRef")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? TypeRef { get; set; } = default;

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
