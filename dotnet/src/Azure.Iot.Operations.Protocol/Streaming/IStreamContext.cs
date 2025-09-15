// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// A stream of requests or responses that can be gracefully ended or canceled (with confirmation) at any time.
    /// </summary>
    /// <typeparam name="T">The type of the payload of the request/response stream</typeparam>
    public interface IStreamContext<T>
        where T : class
    {
        /// <summary>
        /// The asynchronously readable entries in the stream
        /// </summary>
        IAsyncEnumerable<T> Entries { get; set; }

        /// <summary>
        /// Cancel this RPC streaming exchange.
        /// </summary>
        /// <param name="userData">
        /// The optional user properties to include in this cancellation request. the receiving side of this cancellation request
        /// will be given these properties alongside the notification that the streaming exchange has been canceled.
        /// </param>
        /// <param name="cancellationToken">Cancellation token to wait for confirmation from the receiving side that the cancellation succeeded.</param>
        /// <remarks>
        /// When called by the invoker, the executor will be notified about this cancellation and the executor will attempt
        /// to stop any user-defined handling of the streaming request. When called by the executor, the invoker will be notified
        /// and will cease sending requests and will throw an <see cref="AkriMqttException"/> with <see cref="AkriMqttException.Kind"/>
        /// of <see cref="AkriMqttErrorKind.Cancellation"/>.
        /// 
        /// This method may be called by the streaming invoker or executor at any time. For instance, if the request stream
        /// stalls unexpectedly, the executor can call this method to notify the invoker to stop sending requests.
        /// Additionally, the invoker can call this method if its response stream has stalled unexpectedly.
        /// </remarks>
        Task CancelAsync(Dictionary<string, string>? userData = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// The token that tracks if the streaming exchange has been cancelled by the other party and/or timed out.
        /// </summary>
        /// <remarks>
        /// For instance, if the invoker side cancels the streaming exchange, the executor side callback's <see cref="IStreamContext{T}.CancellationToken"/>
        /// will be triggered. If the executor side cancels the streaming exchange, the invoker side's returned <see cref="IStreamContext{T}.CancellationToken"/>
        /// will be triggered.
        ///
        /// To see if this was triggered because the stream exchange was cancelled, see <see cref="IsCanceled"/>. To see if it was triggered because
        /// the stream exchange timed out, see <see cref="HasTimedOut"/>.
        /// </remarks>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// Get the user properties associated with a cancellation request started with <see cref="CancelAsync(Dictionary{string, string}?, CancellationToken)"/>.
        /// </summary>
        /// <returns>The user properties associated with a cancellation request</returns>
        /// <remarks>
        /// If the stream has not been cancelled, this will return null. If the stream has been cancelled, but no user properties were
        /// provided in that cancellation request, this will return null.
        /// </remarks>
        Dictionary<string, string>? GetCancellationRequestUserProperties();

        /// <summary>
        /// True if this stream exchange has timed out. If a stream has timed out, <see cref="CancellationToken"/> will trigger as well.
        /// </summary>
        bool HasTimedOut { get; internal set; }

        /// <summary>
        /// True if this stream exchange has been canceled by the other party. If a stream has been cancelled, <see cref="CancellationToken"/> will trigger as well.
        /// </summary>
        bool IsCanceled { get; internal set; }
    }
}
