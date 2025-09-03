// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// Metadata for a specific message within a request stream
    /// </summary>
    public class StreamMessageMetadata
    {
        /// <summary>
        /// The timestamp attached to this particular message
        /// </summary>
        public HybridLogicalClock? Timestamp { get; internal set; }

        /// <summary>
        /// User properties associated with this particular message
        /// </summary>
        public Dictionary<string, string> UserData { get; } = new();

        /// <summary>
        /// The index of this message within the stream as a whole
        /// </summary>
        /// <remarks>This value is automatically assigned when sending messages in a request/response stream and cannot be overriden.</remarks>
        public int Index { get; internal set; }
    }
}
