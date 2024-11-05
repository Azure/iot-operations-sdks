using Azure.Iot.Operations.Services.AzureDeviceRegistry;

namespace ConnectorAppProjectTemplate
{
    internal class DatasetSampler : IDatasetSampler
    {
        public Task<byte[]> SampleAsync(Dataset dataset, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
