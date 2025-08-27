// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    public interface ICancelableResponseStreamContext<T>
        where T : class
    {
        IAsyncEnumerable<StreamingExtendedResponse<T>> Responses { get; set; }

        Task CancelAsync(CancellationToken cancellationToken = default);
    }
}
