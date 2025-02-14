// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;

namespace EventDrivenTelemetryConnector
{
    internal class MessageSchemaProviderFactory : IMessageSchemaProviderFactory
    {
        public static Func<IServiceProvider, IMessageSchemaProviderFactory> EventMessageSchemaFactoryProvider = service =>
        {
            return new MessageSchemaProviderFactory();
        };

        public IMessageSchemaProvider CreateMessageSchemaProvider(AssetEndpointProfile assetEndpointProfile, Asset asset)
        {
            throw new NotImplementedException();
        }
    }
}
