namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class AssetDatasetSchemaElement : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'dataPoints' Field.
        /// </summary>
        [JsonPropertyName("dataPoints")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<AssetDatasetDataPointSchemaElement>? DataPoints { get; set; } = default;

        /// <summary>
        /// Stringified JSON that contains connector-specific JSON string that describes configuration for the specific dataset.
        /// </summary>
        [JsonPropertyName("datasetConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? DatasetConfiguration { get; set; } = default;

        /// <summary>
        /// The address of the source of the data in the dataset (e.g. URL) so that a client can access the data source on the asset.
        /// </summary>
        [JsonPropertyName("dataSource")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? DataSource { get; set; } = default;

        /// <summary>
        /// Destinations for a Dataset.
        /// </summary>
        [JsonPropertyName("destinations")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<DatasetDestination>? Destinations { get; set; } = default;

        /// <summary>
        /// Name of the dataset.
        /// </summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string Name { get; set; } = default!;

        /// <summary>
        /// URI or type definition id in companion spec.
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
