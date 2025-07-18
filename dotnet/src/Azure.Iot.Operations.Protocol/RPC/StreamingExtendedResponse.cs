// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public class StreamingExtendedResponse<TResp> : ExtendedResponse<TResp>
        where TResp : class
    {
        /// <summary>
        /// An optional Id for this response (relative to the other responses in this response stream)
        /// </summary>
        /// <remarks>
        /// Users are allowed to provide Ids for each response, only for specific responses, or for none of the responses.
        /// </remarks>
        public string? StreamingResponseId { get; set; }

        /// <summary>
        /// The index of this response relative to the other responses in this response stream. Starts at 0.
        /// </summary>
        public int StreamingResponseIndex { get; set; }

        /// <summary>
        /// If true, this response is the final response in this response stream.
        /// </summary>
        public bool IsLastResponse { get; set; }
    }
}
