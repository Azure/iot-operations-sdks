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
    internal class CancelableStreamContext : ICancelableStreamContext
    {
        private Func<CancellationToken, Task> _cancellationFunc;

        public CancelableStreamContext(Func<CancellationToken, Task> cancellationFunc)
        {
            _cancellationFunc = cancellationFunc;
        }

        public Task CancelAsync(CancellationToken cancellationToken = default)
        {
            return _cancellationFunc.Invoke(cancellationToken);
        }
    }
}
