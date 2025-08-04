// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public interface ICancelableAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        Task CancelAsync();
    }
}
