// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// The payload and metadata associated with a single request in a request stream.
    /// </summary>
    /// <typeparam name="TReq">The type of the payload of the request</typeparam>
    public class ReceivedStreamingExtendedRequest<TReq> : StreamingExtendedRequest<TReq>
        where TReq : class
    {
        private readonly Task _acknowledgementFunc;

        internal ReceivedStreamingExtendedRequest(TReq request, StreamMessageMetadata metadata, Task acknowledgementFunc)
            : base(request, metadata)
        {
            _acknowledgementFunc = acknowledgementFunc;
        }

        public async Task AcknowledgeAsync()
        {
            await _acknowledgementFunc;
        }
    }
}
