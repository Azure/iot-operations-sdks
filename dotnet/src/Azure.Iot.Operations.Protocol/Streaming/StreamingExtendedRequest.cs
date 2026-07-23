// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// The payload and metadata associated with a single request in a request stream.
    /// </summary>
    /// <typeparam name="TReq">The type of the payload of the request</typeparam>
    public class StreamingExtendedRequest<TReq>
        where TReq : class
    {
        /// <summary>
        /// The request payload
        /// </summary>
        public TReq Payload { get; set; }

        /// <summary>
        /// The metadata specific to this message in the stream
        /// </summary>
        public StreamMessageMetadata Metadata { get; set; }

        /// <summary>
        /// How long the message will be persisted by the MQTT broker if the executor side is not connected to receive it.
        /// </summary>
        /// <remarks>
        /// By default, this value will be set equal to the stream-level timeout specified in <see cref="StreamingCommandInvoker{TReq, TResp}.InvokeStreamingCommandAsync(System.Collections.Generic.IAsyncEnumerable{StreamingExtendedRequest{TReq}}, RequestStreamMetadata?, System.Collections.Generic.Dictionary{string, string}?, TimeSpan?, System.Threading.CancellationToken)"/>.
        /// Generally, this value should be strictly less than or equal to the stream-level timeout.
        /// Setting shorter timespans here allows for streamed messages to expire if they are no longer relevant beyond a certain point.
        /// </remarks>
        public TimeSpan? MessageExpiry { get; set; }

        public StreamingExtendedRequest(TReq request, StreamMessageMetadata? metadata = null)
        {
            Payload = request;
            Metadata = metadata ?? new();
        }
    }
}
