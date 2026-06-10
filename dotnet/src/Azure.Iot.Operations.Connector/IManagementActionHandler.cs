// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// User-implemented handler for management action invocations. The base
    /// <see cref="ConnectorWorker"/> dispatches every incoming request for the
    /// action to <see cref="HandleAsync"/>; the action's
    /// <see cref="Services.AssetAndDeviceRegistry.Models.AssetManagementGroupActionType"/> is
    /// available on <see cref="ManagementActionInvokedEventArgs.ActionType"/> for handlers that
    /// want to differentiate Call / Read / Write semantics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations are created per management action via <see cref="IManagementActionHandlerFactory"/>.
    /// The base connector manages executor lifecycle, notification handling, health reporting,
    /// and drain logic — the handler only needs to execute the business logic against the device.
    /// </para>
    /// <para>
    /// The SDK is deliberately non-opinionated about Call / Read / Write payload shapes: all
    /// three carry request and response payloads and are routed identically. Whether (and how)
    /// to constrain payloads per type is up to the connector author.
    /// </para>
    /// </remarks>
    public interface IManagementActionHandler : IAsyncDisposable
    {
        /// <summary>
        /// Handle a management action invocation.
        /// </summary>
        /// <param name="args">
        /// Details about the invocation including payload, metadata, and the action type
        /// (<see cref="ManagementActionInvokedEventArgs.ActionType"/>).
        /// </param>
        /// <param name="cancellationToken">
        /// Signaled when the request is no longer applicable (action deleted/replaced, asset
        /// unavailable, connector shutting down). Implementations should abort device I/O promptly.
        /// </param>
        /// <returns>The response to send back to the invoker.</returns>
        Task<ManagementActionResponse> HandleAsync(ManagementActionInvokedEventArgs args, CancellationToken cancellationToken);
    }
}
