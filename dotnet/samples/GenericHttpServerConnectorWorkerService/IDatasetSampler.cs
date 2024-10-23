using Azure.Iot.Operations.Services.AzureDeviceRegistry;

namespace Azure.Iot.Operations.GenericHttpConnectorSample
{
    // TODO Since the design is for this to handle sampling/serializing for X datasets, I should probably add another dataset to show how that would work.
    public interface IDatasetSampler
    {
        //TODO something like this to be more generic
        public Task<byte[]> SampleAsync(AssetEndpointProfile assetEndpointProfile, Dataset dataset, CancellationToken cancellationToken = default);
    }
}
