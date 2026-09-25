// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// The interface for a connector to request message schema information about datasets and/or events
    /// that is registered with the Edge Registry rather than the Schema Registry.
    /// </summary>
    /// <remarks>
    /// Provide an implementation of this interface instead of <see cref="IMessageSchemaProvider"/> when
    /// your deployment of AIO has the Edge Registry service. When both are provided, this one wins.
    /// </remarks>
    public interface IEdgeRegistryMessageSchemaProvider
    {
        /// <summary>
        /// Get the message schema associated with this dataset. If provided, the connector will register this message schema prior to forwarding any dataset telemetry for this dataset.
        /// </summary>
        /// <param name="deviceName">The name of the device this dataset will be sampled from.</param>
        /// <param name="device">The device this dataset will be sampled from.</param>
        /// <param name="inboundEndpointName">The name of the inbound endpoint this dataset will be sampled from.</param>
        /// <param name="assetName">The name of the asset this dataset belongs to.</param>
        /// <param name="asset">The asset this dataset belongs to.</param>
        /// <param name="datasetName">The name of the dataset.</param>
        /// <param name="dataset">The dataset.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The message schema to register for data sampled from this dataset. If null, no message schema will be registered for this dataset.</returns>
        Task<ConnectorEdgeRegistryMessageSchema?> GetMessageSchemaAsync(string deviceName, Device device, string inboundEndpointName, string assetName, Asset asset, string datasetName, AssetDataset dataset, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the message schema associated with this event. If provided, the connector will register this message schema prior to forwarding any event telemetry for this event.
        /// </summary>
        /// <param name="deviceName">The name of the device this event will be received from.</param>
        /// <param name="device">The device this event will be received from.</param>
        /// <param name="inboundEndpointName">The name of the inbound endpoint this event will be received from.</param>
        /// <param name="assetName">The name of the asset this event belongs to.</param>
        /// <param name="asset">The asset this event belongs to.</param>
        /// <param name="eventGroupName">The name of the event group the event belongs to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="assetEvent">The event</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The message schema to register for data received from this event. If null, no message schema will be registered for this event.</returns>
        Task<ConnectorEdgeRegistryMessageSchema?> GetMessageSchemaAsync(string deviceName, Device device, string inboundEndpointName, string assetName, Asset asset, string eventGroupName, string eventName, AssetEvent assetEvent, CancellationToken cancellationToken = default);
    }
}
