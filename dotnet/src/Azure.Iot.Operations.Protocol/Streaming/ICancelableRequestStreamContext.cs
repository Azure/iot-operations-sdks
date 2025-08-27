// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    public interface ICancelableRequestStreamContext<T>
        where T : class
    {
        IAsyncEnumerable<StreamingExtendedRequest<T>> Requests { get; set; }

        Task CancelAsync(CancellationToken cancellationToken = default);
    }
}
