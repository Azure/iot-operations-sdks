// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public interface IMessageSchemaProvider
    {
        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(AssetEndpointProfile assetEndpointProfile, Asset asset, string datasetName, Dataset dataset, CancellationToken cancellationToken = default);

        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(AssetEndpointProfile assetEndpointProfile, Asset asset, string eventName, Event assetEvent, CancellationToken cancellationToken = default);
    }
}
