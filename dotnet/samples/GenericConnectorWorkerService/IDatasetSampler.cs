using Azure.Iot.Operations.Services.AzureDeviceRegistry;

namespace Azure.Iot.Operations.GenericHttpConnectorSample
{
    public interface IDatasetSampler
    {
        public Task<byte[]> SampleAsync(AssetEndpointProfile assetEndpointProfile, Dataset dataset, CancellationToken cancellationToken = default);
    }
}
