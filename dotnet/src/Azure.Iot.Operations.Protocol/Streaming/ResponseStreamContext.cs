// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    // The response stream handed back to the caller: the readable entries plus the awaitable stream-level metadata.
    internal sealed class ResponseStreamContext<T> : IResponseStreamContext<T>
        where T : class
    {
        public IAsyncEnumerable<T> Entries { get; set; }

        public Task<ResponseStreamMetadata> StreamMetadata { get; }

        public ResponseStreamContext(IAsyncEnumerable<T> entries, Task<ResponseStreamMetadata> streamMetadata)
        {
            Entries = entries;
            StreamMetadata = streamMetadata;
        }
    }
}
