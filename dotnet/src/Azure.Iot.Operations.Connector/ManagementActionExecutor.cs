// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Executor for a single management action. Thin wrapper over the MQTT RPC command
    /// executor (<c>CommandExecutor&lt;byte[], byte[]&gt;</c>) subscribed to the action's
    /// request topic. Obtain instances via
    /// <see cref="AssetClient.GetManagementActionExecutorAsync(string, string, System.Threading.CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The executor is callback-shaped, mirroring the underlying
    /// <c>CommandExecutor.OnCommandReceived</c> contract: set
    /// <see cref="OnRequestReceived"/> once after acquiring the executor (or after a swap
    /// triggered by a <see cref="ManagementActionUpdatedWithNewExecutor"/> notification),
    /// and the callback will be invoked once per incoming management action request.
    /// The returned <see cref="ManagementActionResponse"/> is sent back to the invoker.
    /// </para>
    /// <para>
    /// Exceptions thrown by the callback are surfaced to the invoker as
    /// <see cref="ManagementActionApplicationError"/> responses by the connector worker; see
    /// <see cref="ManagementActionConnectorWorker"/>.
    /// </para>
    /// </remarks>
    public sealed class ManagementActionExecutor : IAsyncDisposable
    {
        internal ManagementActionExecutor()
        {
            // Constructed by AssetClient / ConnectorWorker; wraps the underlying CommandExecutor.
        }

        /// <summary>
        /// Invoked once per management action request. The returned
        /// <see cref="ManagementActionResponse"/> is sent to the invoker.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Must be set before the executor begins dispatching. Requests that arrive while
        /// this property is <c>null</c> are replied to with an
        /// <see cref="ManagementActionApplicationError"/> (<c>HandlerNotConfigured</c>) so
        /// invokers see a deterministic failure rather than a timeout.
        /// </para>
        /// <para>
        /// The <see cref="ManagementActionInvokedEventArgs"/> passed to the callback is
        /// already stamped with the action's group / action / asset / device names &mdash;
        /// the executor has that context from
        /// <see cref="AssetClient.GetManagementActionExecutorAsync(string, string, CancellationToken)"/>.
        /// </para>
        /// <para>
        /// The supplied <see cref="CancellationToken"/> is signalled when the underlying
        /// command execution times out (per the command executor's <c>ExecutionTimeout</c>),
        /// when the executor is being stopped/replaced, or when the asset becomes unavailable.
        /// Handlers should honor it and abort device I/O promptly.
        /// </para>
        /// </remarks>
        public Func<ManagementActionInvokedEventArgs, CancellationToken, Task<ManagementActionResponse>>? OnRequestReceived { get; set; }

        /// <summary>
        /// Tear down the MQTT subscription for this action's request topic. After this
        /// completes the broker delivers no further requests to this executor; any
        /// in-flight <see cref="OnRequestReceived"/> invocations continue until they
        /// return (bounded by the underlying command executor's execution timeout) or
        /// are awaited out by <see cref="DisposeAsync"/>.
        /// </summary>
        /// <remarks>
        /// Intended to be invoked by the Connector SDK itself when this executor becomes
        /// outdated (action deleted, definition replaced with a new topic, asset
        /// unavailable, connector shutting down). User code generally should not call
        /// this directly; await the corresponding
        /// <see cref="ManagementActionNotification"/> and let the worker drive the
        /// lifecycle.
        /// </remarks>
        public ValueTask StopAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        /// <summary>
        /// Wait for any in-flight <see cref="OnRequestReceived"/> invocations to complete
        /// and release local resources owned by this executor (callback registrations,
        /// internal queues). Does not unsubscribe from MQTT &mdash; that is
        /// <see cref="StopAsync"/>'s job and must already have happened by the time the
        /// caller disposes. Does not dispose the underlying MQTT client (shared with the
        /// rest of the connector).
        /// </summary>
        public ValueTask DisposeAsync() => throw new NotImplementedException();
    }
}

