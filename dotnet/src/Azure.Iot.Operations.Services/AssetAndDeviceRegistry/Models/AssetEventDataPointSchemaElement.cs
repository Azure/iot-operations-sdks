namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    public partial class AssetEventDataPointSchemaElement : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// Stringified JSON that contains connector-specific configuration for the data point.
        /// </summary>
        [JsonPropertyName("dataPointConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public JsonDocument? DataPointConfiguration { get; set; } = default;

        /// <summary>
        /// The address of the source of the data in the event (e.g. URL) so that a client can access the data source on the asset.
        /// </summary>
        [JsonPropertyName("dataSource")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string DataSource { get; set; } = default!;

        /// <summary>
        /// The name of the data point.
        /// </summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string Name { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (DataSource is null)
            {
                throw new ArgumentNullException("dataSource field cannot be null");
            }
            if (Name is null)
            {
                throw new ArgumentNullException("name field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (DataSource is null)
            {
                throw new ArgumentNullException("dataSource field cannot be null");
            }
            if (Name is null)
            {
                throw new ArgumentNullException("name field cannot be null");
            }
        }
    }
}
