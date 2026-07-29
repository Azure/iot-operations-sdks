// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    // A consumed stream (request or response) exposed to a handler as readable entries.
    internal sealed class StreamContext<T> : IStreamContext<T>
        where T : class
    {
        public IAsyncEnumerable<T> Entries { get; set; }

        public StreamContext(IAsyncEnumerable<T> entries)
        {
            Entries = entries;
        }
    }
}
