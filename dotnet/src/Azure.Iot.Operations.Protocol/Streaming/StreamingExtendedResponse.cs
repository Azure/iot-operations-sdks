// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// The payload and metadata associated with a single response in a response stream.
    /// </summary>
    /// <typeparam name="TResp">The type of the payload of the response</typeparam>
    public class StreamingExtendedResponse<TResp>
        where TResp : class
    {
        /// <summary>
        /// The response payload
        /// </summary>
        public TResp Payload { get; set; }

        /// <summary>
        /// The metadata specific to this message in the stream
        /// </summary>
        public StreamMessageMetadata Metadata { get; set; }

        /// <summary>
        /// How long the message will be persisted by the MQTT broker if the invoker side is not connected to receive it.
        /// </summary>
        /// <remarks>
        /// By default, this value will be set equal to the stream-level timeout specified in <see cref="StreamingCommandInvoker{TReq, TResp}.InvokeStreamingCommandAsync(System.Collections.Generic.IAsyncEnumerable{StreamingExtendedRequest{TReq}}, RequestStreamMetadata?, System.Collections.Generic.Dictionary{string, string}?, TimeSpan?, System.Threading.CancellationToken)"/>.
        /// Generally, this value should be strictly less than or equal to the stream-level timeout.
        /// Setting shorter timespans here allows for streamed messages to expire if they are no longer relevant beyond a certain point.
        /// </remarks>
        public TimeSpan? MessageExpiry { get; set; }

        public StreamingExtendedResponse(TResp response, StreamMessageMetadata? metadata = null)
        {
            Payload = response;
            Metadata = metadata ?? new();
        }
    }
}
