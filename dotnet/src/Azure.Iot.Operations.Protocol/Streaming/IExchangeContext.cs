// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// The per-exchange lifecycle and control handle for a single streaming RPC invocation: completion,
    /// cancellation, and timeout. It is exchange-scoped (one per invocation, shared across the request and
    /// response streams) rather than per-stream, which keeps <see cref="IStreamContext{T}"/> symmetric.
    /// </summary>
    public interface IExchangeContext
    {
        /// <summary>
        /// Completes when the exchange finishes gracefully - both half-streams have closed (the invoker has
        /// sent its final request and received the final response, and vice-versa for the executor). Faults or
        /// is canceled when the exchange terminates for any other reason, including a local send failure.
        /// </summary>
        Task Completion { get; }

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
        /// The token that tracks if this streaming exchange has been cancelled by the other party and/or timed out.
        /// </summary>
        /// <remarks>
        /// For instance, if the invoker side cancels the streaming exchange, the executor side callback's <see cref="CancellationToken"/>
        /// will be triggered. If the executor side cancels the streaming exchange, the invoker side's returned <see cref="CancellationToken"/>
        /// will be triggered.
        ///
        /// To see if this was triggered because the exchange was cancelled, see <see cref="IsCanceled"/>. To see if it was triggered because
        /// the exchange timed out locally, see <see cref="HasTimedOut"/>.
        /// </remarks>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// Get the user properties associated with a cancellation request started with <see cref="CancelAsync(Dictionary{string, string}?, CancellationToken)"/>.
        /// </summary>
        /// <returns>The user properties associated with a cancellation request</returns>
        /// <remarks>
        /// If the exchange has not been cancelled, this will return null. If the exchange has been cancelled, but no user properties were
        /// provided in that cancellation request, this will return null.
        /// </remarks>
        Dictionary<string, string>? GetCancellationRequestUserProperties();

        /// <summary>
        /// True if this exchange has timed out locally. If an exchange has timed out, <see cref="CancellationToken"/> will trigger as well.
        /// </summary>
        /// <remarks>
        /// A timeout is a local idle-timeout event; no timeout status is exchanged over the wire, so each side reaches its own timeout independently.
        /// </remarks>
        bool HasTimedOut { get; internal set; }

        /// <summary>
        /// True if this exchange has been canceled by the other party. If an exchange has been cancelled, <see cref="CancellationToken"/> will trigger as well.
        /// </summary>
        bool IsCanceled { get; internal set; }
    }
}
