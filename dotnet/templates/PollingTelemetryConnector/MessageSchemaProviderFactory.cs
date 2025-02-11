// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;
using ConnectorApp;

namespace PollingTelemetryConnector
{
    internal class MessageSchemaProviderFactory : IMessageSchemaProviderFactory
    {
        public static Func<IServiceProvider, IMessageSchemaProviderFactory> DatasetMessageSchemaFactoryProvider = service =>
        {
            return new MessageSchemaProviderFactory();
        };

        public IMessageSchemaProvider CreateMessageSchemaProvider(AssetEndpointProfile assetEndpointProfile, Asset asset)
        {
            throw new NotImplementedException();
        }
    }
}
