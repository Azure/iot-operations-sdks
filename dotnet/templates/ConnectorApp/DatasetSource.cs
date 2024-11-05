using Azure.Iot.Operations.Services.AzureDeviceRegistry;

namespace ConnectorApp
{
    internal class DatasetSource : IDatasetSource
    {
        public Task<byte[]> SampleAsync(Dataset dataset, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
