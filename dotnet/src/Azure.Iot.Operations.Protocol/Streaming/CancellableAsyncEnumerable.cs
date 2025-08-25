// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    internal class CancellableAsyncEnumerable<T> : ICancellableAsyncEnumerable<T>
    {
        private readonly Func<Guid, CancellationToken, Task> _cancellationFunction;

        internal CancellableAsyncEnumerable(Func<Guid, CancellationToken, Task> cancellationFunction, IAsyncEnumerable<T> asyncEnumerable)
        {
            _cancellationFunction = cancellationFunction;
            AsyncEnumerable = asyncEnumerable;
        }

        public IAsyncEnumerable<T> AsyncEnumerable { get; set; }

        public Task CancelAsync(Guid correlationId, CancellationToken cancellationToken = default)
        {
            return _cancellationFunction.Invoke(correlationId, cancellationToken);
        }
    }
}
