// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;

namespace EventDrivenRestThermostatConnector
{
    internal class DatasetMessageSchemaProviderFactory : IDatasetMessageSchemaProviderFactory
    {
        public static Func<IServiceProvider, IDatasetMessageSchemaProviderFactory> DatasetMessageSchemaFactoryProvider = service =>
        {
            return new DatasetMessageSchemaProviderFactory();
        };

        public IDatasetMessageSchemaProvider CreateDatasetMessageSchemaProvider(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset)
        {
            // No datasets in this sample will register a message schema.
            return new DatasetMessageSchemaProvider();
        }
    }
}
