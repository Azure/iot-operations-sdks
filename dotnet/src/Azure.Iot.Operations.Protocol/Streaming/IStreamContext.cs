// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// A consumed stream in a streaming RPC exchange - the asynchronously readable entries of a request or response
    /// stream that you receive. (Streams you produce are passed as a plain <see cref="IAsyncEnumerable{T}"/>.)
    /// Exchange-scoped lifecycle and control (completion, cancellation, timeout) live on <see cref="IExchangeContext"/>, not here.
    /// </summary>
    /// <typeparam name="T">The type of the payload of the request/response stream</typeparam>
    public interface IStreamContext<T>
        where T : class
    {
        /// <summary>
        /// The asynchronously readable entries in the stream
        /// </summary>
        IAsyncEnumerable<T> Entries { get; set; }
    }
}
