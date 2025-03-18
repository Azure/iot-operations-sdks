// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System;
    using System.Threading;

    public class PausableCancellationTokenSource : Pausable<TriggerableCancellationTokenSource>, IPausable
    {
        public CancellationTokenSource TokenSource { get => _source.TokenSource; }

        public PausableCancellationTokenSource(TimeSpan delay, bool startPaused)
            : base(delay, startPaused)
        {
        }
    }
}
