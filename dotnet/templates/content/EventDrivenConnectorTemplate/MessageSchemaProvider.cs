// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace EventDrivenTelemetryConnector
{
    /// <summary>
    /// The factory method for determining what message schema should be registered for and attached to each
    /// emitted telemetry message for each event handled by this connector. If no message schema is needed,
    /// then this class can be left as-is.
    /// </summary>
    /// <remarks>
    /// Registering message schemas allows the receiver of a message to fetch this schema from the schema
    /// registry service. With that schema, the receiver can correctly deserialize each received message's payload.
    /// </remarks>
    internal class MessageSchemaProvider : IMessageSchemaProvider
    {
        public static Func<IServiceProvider, IMessageSchemaProvider> Factory = service =>
        {
            return new MessageSchemaProvider();
        };

        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(Device device, Asset asset, string datasetName, AssetDataset dataset, CancellationToken cancellationToken = default)
        {
            // By returning null, no message schema will be registered for telemetry sent for this dataset.
            return Task.FromResult((ConnectorMessageSchema?)null);
        }

        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(Device device, Asset asset, string eventName, AssetEvent assetEvent, CancellationToken cancellationToken = default)
        {
            // By returning null, no message schema will be registered for telemetry sent for this event.
            return Task.FromResult((ConnectorMessageSchema?)null);
        }
    }
}
