// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Factory for creating <see cref="IManagementActionHandler"/> instances, one per management
    /// action defined on an asset. Called by <see cref="ConnectorWorker"/> when
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
        /// <param name="statusReporter">
        /// Per-action status-reporting hook the handler may retain and call when it discovers
        /// runtime issues (e.g. target URI becomes unreachable) or recovers from them.
        /// Configuration-time validation should instead be returned from
        /// <see cref="ValidateConfigurationAsync"/>; this reporter is for runtime transitions
        /// that occur after the action is up.
        /// </param>
        /// <returns>
        /// A handler that will receive invocations for this action. The base connector will
        /// dispose it when the action is deleted or the asset becomes unavailable.
        /// </returns>
        IManagementActionHandler CreateHandler(
            Device device,
            string inboundEndpointName,
            Asset asset,
            AssetManagementGroupAction action,
            EndpointCredentials? endpointCredentials,
            IManagementActionStatusReporter statusReporter);

        /// <summary>
        /// Perform connector-specific validation of an action's definition. Called by the
        /// base worker at startup and on every definition-change notification, before any
        /// invocations are dispatched. Return <c>null</c> if the definition is valid from
        /// the connector's perspective.
        /// </summary>
        /// <remarks>
        /// The SDK performs structural validation (well-formed topic, required fields, etc.)
        /// and surfaces those errors via <c>ManagementActionUpdated.Error</c>. This hook
        /// lets the connector application add semantic validation that only it can perform —
        /// for example, that <c>action.TargetUri</c> uses a scheme this connector supports,
        /// that <c>action.ActionConfiguration</c> deserializes into the connector's expected
        /// shape, or that referenced asset attributes exist.
        /// <para>
        /// The base worker merges this result with the SDK-supplied <see cref="ConfigError"/>
        /// before reporting to ADR; if either is non-null the action is reported Unavailable.
        /// </para>
        /// <para>
        /// The default implementation returns <c>null</c> (no connector-specific validation).
        /// Override to opt in.
        /// </para>
        /// <para>
        /// Note: unlike most <c>Validate*</c> methods in this SDK (which throw on invalid input),
        /// this hook <em>returns</em> the error. Validation results are reported back to ADR as
        /// <see cref="ConfigError"/>s, so returning one keeps the shape symmetric with the
        /// SDK-supplied error the worker merges it with, and avoids forcing the notification
        /// loop to catch-and-unwrap exceptions on every definition change.
        /// </para>
        /// </remarks>
        ValueTask<ConfigError?> ValidateConfigurationAsync(
            Device device,
            string inboundEndpointName,
            Asset asset,
            AssetManagementGroupAction action,
            CancellationToken cancellationToken)
            => ValueTask.FromResult<ConfigError?>(null);

        bool SupportsAction(AssetManagementGroupAction action);
    }
}
