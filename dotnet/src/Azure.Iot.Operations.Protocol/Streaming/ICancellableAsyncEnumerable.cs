// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    public interface ICancellableAsyncEnumerable<T>
    {
        IAsyncEnumerable<T> AsyncEnumerable { get; set; }

        Task CancelAsync(Guid correlationId, CancellationToken cancellationToken = default);
    }
}
