// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    internal class MockMessageSchemaProvider : IMessageSchemaProvider
    {
        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(Device device, Asset asset, string datasetName, AssetDatasetSchemaElement dataset, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((ConnectorMessageSchema?)null);
        }

        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(Device device, Asset asset, string eventName, AssetEventSchemaElement assetEvent, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((ConnectorMessageSchema?)null);
        }
    }
}
