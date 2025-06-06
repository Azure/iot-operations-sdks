// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace EventDrivenTelemetryConnector
{
    internal class MessageSchemaProvider : IMessageSchemaProvider
    {
        public static Func<IServiceProvider, IMessageSchemaProvider> MessageSchemaProviderFactory = service =>
        {
            return new MessageSchemaProvider();
        };

        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(Device device, Asset asset, string datasetName, AssetDatasetSchemaElement dataset, CancellationToken cancellationToken = default)
        {
            // By returning null, no message schema will be registered for telemetry sent for this dataset.
            return Task.FromResult((ConnectorMessageSchema?)null);
        }

        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(Device device, Asset asset, string eventName, AssetEventSchemaElement assetEvent, CancellationToken cancellationToken = default)
        {
            // By returning null, no message schema will be registered for telemetry sent for this event.
            return Task.FromResult((ConnectorMessageSchema?)null);
        }
    }
}
