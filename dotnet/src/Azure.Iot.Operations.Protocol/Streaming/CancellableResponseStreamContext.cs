// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    internal class CancellableResponseStreamContext<T> : ICancelableResponseStreamContext<T>
        where T : class
    {
        private readonly Func<CancellationToken, Task> _cancellationFunction;

        internal CancellableResponseStreamContext(Func<CancellationToken, Task> cancellationFunction, IAsyncEnumerable<StreamingExtendedResponse<T>> asyncEnumerable)
        {
            _cancellationFunction = cancellationFunction;
            Responses = asyncEnumerable;
        }

        public IAsyncEnumerable<StreamingExtendedResponse<T>> Responses { get; set; }

        public Task CancelAsync(CancellationToken cancellationToken = default)
        {
            return _cancellationFunction.Invoke(cancellationToken);
        }
    }
}
