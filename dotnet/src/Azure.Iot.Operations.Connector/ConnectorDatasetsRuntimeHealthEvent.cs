namespace Azure.Iot.Operations.Connector
{
    using System;

    public partial class ConnectorDatasetsRuntimeHealthEvent
    {
        /// <summary>
        /// The name of the dataset for which the runtime health is being reported.
        /// </summary>
        public string DatasetName { get; set; } = default!;

        /// <summary>
        /// The runtime health of the specific dataset.
        /// </summary>
        public ConnectorRuntimeHealth RuntimeHealth { get; set; } = default!;
    }
}
