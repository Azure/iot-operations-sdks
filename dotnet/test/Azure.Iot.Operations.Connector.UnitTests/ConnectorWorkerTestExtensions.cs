// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.UnitTests
{
    internal static class ConnectorWorkerTestExtensions
    {
        /// <summary>
        /// Stops the worker with a bounded timeout so a stalled shutdown fails fast instead of hanging
        /// indefinitely, then disposes the worker. The timeout <see cref="CancellationTokenSource"/> is
        /// disposed, and the worker is disposed even if <c>StopAsync</c> throws.
        /// </summary>
        public static async Task StopAndDisposeAsync(this ConnectorWorker worker)
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
            try
            {
                await worker.StopAsync(cts.Token);
            }
            finally
            {
                worker.Dispose();
            }
        }
    }
}
