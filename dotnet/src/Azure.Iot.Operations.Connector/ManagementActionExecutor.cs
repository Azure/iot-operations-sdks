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
        public Task<ManagementActionRequest?> RecvRequestAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        /// <summary>
        /// Stop the underlying <c>CommandExecutor</c>, unsubscribe from the action
        /// request topic, and release resources. Any still-queued requests are
        /// auto-completed with an error response.
        /// </summary>
        public ValueTask DisposeAsync() => throw new NotImplementedException();
    }
}

