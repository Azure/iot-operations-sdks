// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// The interface for a connector to request message schema information about datasets and/or events
    /// </summary>
    /// <remarks>
    /// <see cref="NoMessageSchemaProvider"/> can be used if you do not want to register any message schemas for your datasets or events.
    /// </remarks>
    public interface IMessageSchemaProvider
    {
        /// <summary>
        /// Get the message schema associated with this dataset. If provided, the connector will register this message schema prior to forwarding any dataset telemetry for this dataset..
        /// </summary>
        /// <param name="assetEndpointProfile">The asset endpoint profile this dataset will be sampled from.</param>
        /// <param name="asset">The asset this dataset belongs to.</param>
        /// <param name="datasetName">The name of the dataset.</param>
        /// <param name="dataset">The dataset.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The message schema to register for data sampled from this dataset. If null, no message schema will be registered for this dataset.</returns>
        Task<ConnectorMessageSchema?> GetMessageSchemaAsync(AssetEndpointProfile assetEndpointProfile, Asset asset, string datasetName, Dataset dataset, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the message schema associated with this event. If provided, the connector will register this message schema prior to forwarding any event telemetry for this event.
        /// </summary>
        /// <param name="assetEndpointProfile">The asset endpoint profile this event will be received from.</param>
        /// <param name="asset">The asset this event belongs to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="assetEvent">The event</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The message schema to register for data received from this event. If null, no message schema will be registered for this event.</returns>
        Task<ConnectorMessageSchema?> GetMessageSchemaAsync(AssetEndpointProfile assetEndpointProfile, Asset asset, string eventName, Event assetEvent, CancellationToken cancellationToken = default);
    }
}
