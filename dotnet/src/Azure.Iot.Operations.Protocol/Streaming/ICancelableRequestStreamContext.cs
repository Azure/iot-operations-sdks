// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    public interface ICancelableRequestStreamContext<T>
        where T : class
    {
        /// <summary>
        /// The asynchronously readable requests.
        /// </summary>
        IAsyncEnumerable<StreamingExtendedRequest<T>> Requests { get; set; }

        /// <summary>
        /// Cancel this received RPC streaming request.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for this cancellation request</param>
        /// <remarks>
        /// This method may be called by the streaming executor at any time. For instance, if the request stream
        /// stalls unexpectedly, the executor can call this method to notify the invoker to stop sending requests.
        /// Additionally, the executor can call this method if its response stream has stalled unexpectedly.
        /// </remarks>
        Task CancelAsync(CancellationToken cancellationToken = default);
    }
}
