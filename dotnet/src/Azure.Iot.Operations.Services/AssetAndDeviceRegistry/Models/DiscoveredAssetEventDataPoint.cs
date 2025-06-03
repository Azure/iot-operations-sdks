namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class DiscoveredAssetEventDataPoint : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'dataPointConfiguration' Field.
        /// </summary>
        [JsonPropertyName("dataPointConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? DataPointConfiguration { get; set; } = default;

        /// <summary>
        /// The 'dataSource' Field.
        /// </summary>
        [JsonPropertyName("dataSource")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string DataSource { get; set; } = default!;

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
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Name { get; set; } = default;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (DataSource is null)
            {
                throw new ArgumentNullException("dataSource field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (DataSource is null)
            {
                throw new ArgumentNullException("dataSource field cannot be null");
            }
        }
    }
}
