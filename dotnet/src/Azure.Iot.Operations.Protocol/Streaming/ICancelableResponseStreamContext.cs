// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    public interface ICancelableResponseStreamContext<T>
        where T : class
    {
        /// <summary>
        /// The asynchronously readable responses.
        /// </summary>
        IAsyncEnumerable<StreamingExtendedResponse<T>> Responses { get; set; }

        /// <summary>
        /// Cancel this RPC streaming request.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for this cancellation request</param>
        /// <remarks>This method may be called by the streaming invoker at any time. For instance, if the invoker's
        /// request stream has stalled unexpectedly, the invoker can call this to notify the executor that
        /// the RPC has been canceled. Additionally, if the stream of responses is taking longer than expected
        /// or is no longer wanted by the invoker, the invoker may call this to notify the executor to stop sending
        /// responses.</remarks>
        Task CancelAsync(CancellationToken cancellationToken = default);
    }
}
