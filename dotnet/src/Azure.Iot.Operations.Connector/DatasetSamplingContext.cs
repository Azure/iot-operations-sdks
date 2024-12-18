
namespace Azure.Iot.Operations.Connector
{
    internal class DatasetSamplingContext
    {
        internal IDatasetSampler DatasetSampler { get; set; }

        internal Timer DatasetSamplingTimer { get; set; }

        internal DatasetSamplingContext(IDatasetSampler datasetSampler, Timer datasetSamplingTimer)
        {
            DatasetSampler = datasetSampler;
            DatasetSamplingTimer = datasetSamplingTimer;
        }
    }
}
