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
        public TReq Request { get; set; }

        public StreamRequestMessageMetadata RequestMessageMetadata { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    }
}
