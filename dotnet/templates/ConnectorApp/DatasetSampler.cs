using Azure.Iot.Operations.Services.AzureDeviceRegistry;

namespace ConnectorAppProjectTemplate
{
    internal class DatasetSampler : IDatasetSampler
    {
        public Task<byte[]> SampleAsync(Dataset dataset, AssetEndpointProfileCredentials? assetEndpointProfileCredentials = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
