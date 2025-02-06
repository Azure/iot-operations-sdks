// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;
using ConnectorApp;

namespace PollingTelemetryConnector
{
    internal class DatasetMessageSchemaProviderFactory : IDatasetMessageSchemaProviderFactory
    {
        public static Func<IServiceProvider, IDatasetMessageSchemaProviderFactory> DatasetMessageSchemaFactoryProvider = service =>
        {
            return new DatasetMessageSchemaProviderFactory();
        };

        public IDatasetMessageSchemaProvider CreateDatasetMessageSchemaProvider(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset)
        {
            throw new NotImplementedException();
        }
    }
}
