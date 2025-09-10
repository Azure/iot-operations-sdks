// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// A stream of requests or responses that can be gracefully ended or canceled (with confirmation) at any time.
    /// </summary>
    /// <typeparam name="T">The type of the payload of the request stream</typeparam>
    public interface IStreamContext<T>
        where T : class
    {
        /// <summary>
        /// The asynchronously readable entries in the stream
        /// </summary>
        IAsyncEnumerable<T> Entries { get; set; }

        /// <summary>
        /// Cancel this RPC streaming call.
        /// </summary>
        /// <param name="userData">The optional user properties to include in this cancellation request.</param>
        /// <param name="cancellationToken">Cancellation token for this cancellation request</param>
        /// <remarks>
        /// When called by the invoker, the executor will be notified about this cancellation and the executor will attempt
        /// to stop any user-defined handling of the streaming request. When called by the executor, the invoker will be notified
        /// and will cease sending requests.
        /// 
        /// This method may be called by the streaming invoker or executor at any time. For instance, if the request stream
        /// stalls unexpectedly, the executor can call this method to notify the invoker to stop sending requests.
        /// Additionally, the invoker can call this method if its response stream has stalled unexpectedly.
        /// </remarks>
        Task CancelAsync(Dictionary<string, string>? userData = null, CancellationToken cancellationToken = default);

        //TODO how to pass these user properties to the executor when invoker cancels? Triggering cancellation token isn't sufficient

        //TODO move cancellation token in here so that both invoker + executor can access it more seamlessly? Move func in here as well for same reason?
    }
}
