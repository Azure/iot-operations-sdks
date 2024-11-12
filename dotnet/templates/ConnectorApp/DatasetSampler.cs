using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;

namespace ConnectorApp
{
    internal class DatasetSampler : IDatasetSampler
    {
        public Task<byte[]> SampleDatasetAsync(Dataset dataset, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
