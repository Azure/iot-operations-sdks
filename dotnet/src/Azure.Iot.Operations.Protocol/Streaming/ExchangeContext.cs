// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    // Minimal per-exchange handle for the POC: graceful completion only (cancellation and timeout deferred).
    internal sealed class ExchangeContext : IExchangeContext
    {
        private readonly TaskCompletionSource _completion = new();

        public Task Completion => _completion.Task;

        public CancellationToken CancellationToken => CancellationToken.None;

        public bool HasTimedOut { get; set; }

        public bool IsCanceled { get; set; }

        public Task CancelAsync(Dictionary<string, string>? userData = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException("Cancellation is out of scope for the POC.");

        public Dictionary<string, string>? GetCancellationRequestUserProperties() => null;

        // Signals the exchange finished gracefully (both streams closed).
        internal void Complete() => _completion.TrySetResult();
    }
}
