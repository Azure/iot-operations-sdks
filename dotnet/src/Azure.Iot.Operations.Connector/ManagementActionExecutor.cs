// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Executor for a single management action. Wraps an MQTT RPC command executor
    /// (<c>CommandExecutor&lt;byte[], byte[]&gt;</c>) subscribed to the action's request
    /// topic. Obtain instances via
    /// <see cref="AssetClient.GetManagementActionExecutorAsync(string, string, System.Threading.CancellationToken)"/>.
    /// </summary>
    public sealed class ManagementActionExecutor : IAsyncDisposable
    {
        internal ManagementActionExecutor()
        {
            // Constructed by AssetClient / ConnectorWorker; wraps the underlying CommandExecutor.
        }

        /// <summary>
        /// Await the next incoming management action invocation for this executor.
        /// Returns <c>null</c> if the executor has been shut down (no more requests will
        /// be delivered).
        /// </summary>
        /// <param name="cancellationToken">
        /// Cancels the wait, and should also be observed while processing the returned
        /// request: it is signalled when the request is no longer applicable (action
        /// deleted or replaced, asset unavailable, connector shutting down) so the
        /// handler can abort device I/O instead of producing an undeliverable response.
        /// </param>
        public Task<ManagementActionRequest?> RecvRequestAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        /// <summary>
        /// Tear down the MQTT subscription for this action's request topic. After this
        /// completes the broker delivers no further requests to this executor; once any
        /// already-buffered requests have been consumed, <see cref="RecvRequestAsync"/>
        /// returns <c>null</c>.
        /// </summary>
        /// <remarks>
        /// Intended to be invoked by the Connector SDK itself when this executor becomes
        /// outdated (action deleted, definition replaced with a new topic, asset
        /// unavailable, connector shutting down) — eagerly, before the corresponding
        /// <see cref="ManagementActionNotification"/> is surfaced to user code, so the
        /// user's drain loop sees a finite, frozen backlog. User code generally should
        /// not call this; call <see cref="DisposeAsync"/> after draining instead.
        /// Calling it manually mid-flight will leave the owning
        /// <see cref="ConnectorWorker"/>'s per-action bookkeeping out of sync.
        /// </remarks>
        public ValueTask StopAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        /// <summary>
        /// Release local resources owned by this executor (semaphores, callback
        /// registrations, internal queues). Does not unsubscribe from MQTT — that is
        /// <see cref="StopAsync"/>'s job and must already have happened by the time
        /// the user disposes. Does not dispose the underlying MQTT client (shared with
        /// the rest of the connector).
        /// </summary>
        public ValueTask DisposeAsync() => throw new NotImplementedException();
    }
}

