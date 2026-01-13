namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;

    public partial class DatasetsRuntimeHealth
    {
        /// <summary>
        /// The name of the dataset for which the runtime health is being reported.
        /// </summary>
        public string DatasetName { get; set; } = default!;

        /// <summary>
        /// The runtime health of the specific dataset.
        /// </summary>
        public RuntimeHealth RuntimeHealth { get; set; } = default!;
    }
}
