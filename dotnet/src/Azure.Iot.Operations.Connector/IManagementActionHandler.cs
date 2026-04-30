// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// User-implemented handler for management action invocations. The base
    /// <see cref="ManagementActionConnectorWorker"/> dispatches incoming requests to the
    /// appropriate method based on the action's <see cref="Services.AssetAndDeviceRegistry.Models.AssetManagementGroupActionType"/>.
    /// </summary>
    /// <remarks>
    /// Implementations are created per management action via <see cref="IManagementActionHandlerFactory"/>.
    /// The base connector manages executor lifecycle, notification handling, health reporting,
    /// and drain logic — the handler only needs to execute the business logic against the device.
    /// </remarks>
    public interface IManagementActionHandler : IAsyncDisposable
    {
        /// <summary>
        /// Handle a general-purpose "call" action (RPC-style: payload in, payload out).
        /// </summary>
        /// <param name="args">Details about the invocation including payload and metadata.</param>
        /// <param name="cancellationToken">
        /// Signaled when the request is no longer applicable (action deleted/replaced, asset
        /// unavailable, connector shutting down). Implementations should abort device I/O promptly.
        /// </param>
        /// <returns>The response to send back to the invoker.</returns>
        Task<ManagementActionResponse> HandleCallAsync(ManagementActionInvokedEventArgs args, CancellationToken cancellationToken);

        /// <summary>
        /// Handle a "read" action (minimal/no request payload, value payload out).
        /// </summary>
        /// <param name="args">Details about the invocation including payload and metadata.</param>
        /// <param name="cancellationToken">
        /// Signaled when the request is no longer applicable.
        /// </param>
        /// <returns>The response to send back to the invoker containing the read value.</returns>
        Task<ManagementActionResponse> HandleReadAsync(ManagementActionInvokedEventArgs args, CancellationToken cancellationToken);

        /// <summary>
        /// Handle a "write" action (value payload in, minimal/no response).
        /// </summary>
        /// <param name="args">Details about the invocation including the value to write.</param>
        /// <param name="cancellationToken">
        /// Signaled when the request is no longer applicable.
        /// </param>
        /// <returns>The response to send back to the invoker (typically an empty success acknowledgement).</returns>
        Task<ManagementActionResponse> HandleWriteAsync(ManagementActionInvokedEventArgs args, CancellationToken cancellationToken);
    }
}
