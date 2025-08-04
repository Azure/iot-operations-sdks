// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public class StreamingExtendedResponse<TReq> : ExtendedResponse<TReq>
        where TReq : class
    {
        /// <summary>
        /// The index of this response relative to the other responses in this response stream. Starts at 0.
        /// </summary>
        public int StreamingResponseIndex { get; set; }
    }
}
