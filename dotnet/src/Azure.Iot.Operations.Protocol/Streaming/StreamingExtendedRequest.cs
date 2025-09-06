// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        public TReq Request { get; set; }

        /// <summary>
        /// The metadata specific to this message in the stream
        /// </summary>
        public StreamMessageMetadata Metadata { get; set; }

        public StreamingExtendedRequest(TReq request, StreamMessageMetadata? metadata = null)
        {
            Request = request;
            Metadata = metadata ?? new();
        }
    }
}
