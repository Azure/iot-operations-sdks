// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol.RPC;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    public class StreamingExtendedRequest<TReq>
        where TReq : class
    {
        /// <summary>
        /// The index of this request relative to the other requests in this request stream. Starts at 0.
        /// </summary>
        public int StreamingRequestIndex { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        /// <summary>
        /// The request payload
        /// </summary>
        public TReq Request { get; set; }

        /// <summary>
        /// The metadata specific to this message in the stream
        /// </summary>
        public StreamRequestMessageMetadata RequestMessageMetadata { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    }
}
