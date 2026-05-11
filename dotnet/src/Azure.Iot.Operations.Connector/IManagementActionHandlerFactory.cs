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
    /// <para>
    /// The asset / device / group / action <em>names</em> are intentionally not passed here.
    /// They are surfaced on <see cref="ManagementActionInvokedEventArgs"/> for each invocation
    /// so the handler can read/call/write the correct asset on the correct device without
    /// needing to capture them at construction time.
    /// </para>
    /// </remarks>
    public interface IManagementActionHandlerFactory
    {
        /// <summary>
        /// Create a handler for the specified management action.
        /// </summary>
        /// <param name="device">The device model that owns the asset.</param>
        /// <param name="inboundEndpointName">The name of the inbound endpoint used to reach the device.</param>
        /// <param name="asset">The asset model that declares this action.</param>
        /// <param name="action">The management action definition (carries action type, target URI, timeout, etc.).</param>
        /// <param name="endpointCredentials">Credentials for connecting to the device endpoint, if available.</param>
        /// <returns>
        /// A handler that will receive invocations for this action. The base connector will
        /// dispose it when the action is deleted or the asset becomes unavailable.
        /// </returns>
        IManagementActionHandler CreateHandler(
            Device device,
            string inboundEndpointName,
            Asset asset,
            AssetManagementGroupAction action,
            EndpointCredentials? endpointCredentials);
    }
}
