// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;

namespace PollingTelemetryConnector
{
    public class DatasetSamplerFactory : IDatasetSamplerFactory
    {
        public static Func<IServiceProvider, IDatasetSamplerFactory> DatasetSamplerFactoryProvider = service =>
        {
            return new DatasetSamplerFactory();
        };

        public IDatasetSampler CreateDatasetSampler(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset)
        {
            // this method should return the appropriate dataset sampler implementation for the provided asset + dataset. This
            // method may be called multiple times if the asset or dataset changes in any way over time.
            throw new NotImplementedException();
        }
    }
}
