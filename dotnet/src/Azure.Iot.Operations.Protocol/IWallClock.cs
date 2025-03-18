// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol
{
    public interface IWallClock
    {
        DateTime UtcNow { get; }

        CancellationTokenSource CreateCancellationTokenSource(TimeSpan delay);

        Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout, CancellationToken cancellationToken);
    }
}
