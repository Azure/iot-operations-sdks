// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;

namespace EventDrivenRestThermostatConnector
{
    internal class DatasetMessageSchemaProviderFactory : IMessageSchemaProviderFactory
    {
        public static Func<IServiceProvider, IMessageSchemaProviderFactory> DatasetMessageSchemaFactoryProvider = service =>
        {
            return new DatasetMessageSchemaProviderFactory();
        };

        public IMessageSchemaProvider CreateMessageSchemaProvider(AssetEndpointProfile assetEndpointProfile, Asset asset)
        {
            // No datasets in this sample will register a message schema.
            return new MessageSchemaProvider();
        }
    }
}
