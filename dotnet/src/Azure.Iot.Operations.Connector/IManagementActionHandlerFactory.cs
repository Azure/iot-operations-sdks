// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Factory for creating <see cref="IManagementActionHandler"/> instances, one per management
    /// action defined on an asset. Called by <see cref="ManagementActionConnectorWorker"/> when
    /// an asset becomes available.
    /// </summary>
    /// <remarks>
    /// Follows the same factory pattern as <see cref="IDatasetSamplerFactory"/>: per-action
    /// context is passed at creation time so handler instances can capture device connection
    /// details, credentials, and action configuration.
    /// </remarks>
    public interface IManagementActionHandlerFactory
    {
        /// <summary>
        /// Create a handler for the specified management action.
        /// </summary>
        /// <param name="deviceName">The name of the device that holds the asset.</param>
        /// <param name="device">The device model.</param>
        /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
        /// <param name="assetName">The name of the asset that owns this action.</param>
        /// <param name="asset">The asset model.</param>
        /// <param name="groupName">The management group name containing the action.</param>
        /// <param name="action">The management action definition.</param>
        /// <param name="endpointCredentials">Credentials for connecting to the device endpoint, if available.</param>
        /// <returns>
        /// A handler that will receive invocations for this action. The base connector will
        /// dispose it when the action is deleted or the asset becomes unavailable.
        /// </returns>
        IManagementActionHandler CreateHandler(
            string deviceName,
            Device device,
            string inboundEndpointName,
            string assetName,
            Asset asset,
            string groupName,
            AssetManagementGroupAction action,
            EndpointCredentials? endpointCredentials);
    }
}
