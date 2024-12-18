// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    internal class ExecutionDispatcher
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private readonly SemaphoreSlim semaphore;

        public static ExecutionDispatcherCollection CollectionInstance = ExecutionDispatcherCollection.GetCollectionInstance();

        internal ExecutionDispatcher(int maxConcurrency)
        {
            semaphore = new SemaphoreSlim(maxConcurrency);
        }

        internal async Task SubmitAsync(Func<Task>? process, Func<Task> acknowledge)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);

            ThreadPool.UnsafeQueueUserWorkItem(async (_) =>
            {
                try
                {
                    if (process is not null)
                    {
                        await process().ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    Trace.TraceError("Encountered an error while executing an RPC request: {0}", e);
                }

                try
                {
                    await acknowledge().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Trace.TraceError("Encountered an error while acknowledging an RPC request: {0}", e);
                }

                semaphore.Release();
            },
            0,
            preferLocal: false);
        }
    }
}
