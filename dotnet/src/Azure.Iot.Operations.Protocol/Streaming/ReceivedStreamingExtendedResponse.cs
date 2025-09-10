// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// The payload and metadata associated with a single response in a response stream.
    /// </summary>
    /// <typeparam name="TResp">The type of the payload of the response</typeparam>
    public class ReceivedStreamingExtendedResponse<TResp> : StreamingExtendedResponse<TResp>
        where TResp : class
    {
        private readonly Task _acknowledgementFunc;

        internal ReceivedStreamingExtendedResponse(TResp response, StreamMessageMetadata metadata, Task acknowledgementFunc)
            : base(response, metadata)
        {
            _acknowledgementFunc = acknowledgementFunc;
        }

        public async Task AcknowledgeAsync()
        {
            await _acknowledgementFunc;
        }
    }
}
