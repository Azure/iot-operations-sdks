// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    internal class CancellableRequestStreamContext<T> : ICancelableRequestStreamContext<T>
        where T : class
    {
        private readonly Func<CancellationToken, Task> _cancellationFunction;

        internal CancellableRequestStreamContext(Func<CancellationToken, Task> cancellationFunction, IAsyncEnumerable<StreamingExtendedRequest<T>> asyncEnumerable)
        {
            _cancellationFunction = cancellationFunction;
            Requests = asyncEnumerable;
        }

        public IAsyncEnumerable<StreamingExtendedRequest<T>> Requests { get; set; }

        public Task CancelAsync(CancellationToken cancellationToken = default)
        {
            return _cancellationFunction.Invoke(cancellationToken);
        }
    }
}
