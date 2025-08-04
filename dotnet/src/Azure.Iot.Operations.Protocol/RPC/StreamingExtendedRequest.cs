// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public class StreamingExtendedRequest<TReq> : ExtendedRequest<TReq>
        where TReq : class
    {
        /// <summary>
        /// The index of this request relative to the other requests in this request stream. Starts at 0.
        /// </summary>
        public int StreamingRequestIndex { get; set; }

        /// <summary>
        /// If true, this request is the final request in this request stream.
        /// </summary>
        public bool IsLastRequest { get; set; }
    }
}
