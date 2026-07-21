// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// The consumed response stream returned by the invoker, together with an awaitable for the response
    /// stream's stream-level metadata.
    /// </summary>
    /// <typeparam name="T">The type of the payload of the response stream</typeparam>
    public interface IResponseStreamContext<T> : IStreamContext<T>
        where T : class
    {
        /// <summary>
        /// Completes with the response stream's stream-level metadata once the first response is received.
        /// </summary>
        /// <remarks>
        /// The invoker returns before the first response arrives, so the response stream's metadata is not yet known at
        /// return time. Await this to obtain it once the first response is received. It faults if the exchange terminates
        /// (error, cancellation, or timeout) before any response arrives.
        /// </remarks>
        Task<ResponseStreamMetadata> StreamMetadata { get; }
    }
}
