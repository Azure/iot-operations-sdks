
namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;

    public partial class DatasetRuntimeHealthEventSchema
    {
        /// <summary>
        /// The name of the asset containing the datasets for which the runtime health is being reported.
        /// </summary>
        public string AssetName { get; set; } = default!;

        /// <summary>
        /// Array of dataset runtime health information.
        /// </summary>
        public List<DatasetsSchemaElementSchema> Datasets { get; set; } = default!;
    }
}
