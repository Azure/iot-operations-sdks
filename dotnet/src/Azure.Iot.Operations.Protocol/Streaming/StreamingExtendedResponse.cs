// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        public TResp Response { get; set; }

        /// <summary>
        /// The metadata specific to this message in the stream
        /// </summary>
        public StreamMessageMetadata Metadata { get; set; }

        public StreamingExtendedResponse(TResp response, StreamMessageMetadata? metadata = null)
        {
            Response = response;
            Metadata = metadata ?? new();
        }
    }
}
