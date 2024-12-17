// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System;
    using System.Threading.Tasks;

    public class PausableTaskCompletionSource : Pausable<TriggerableTaskCompletionSource>, IPausable
    {
        public Task Task { get => source.Task; }

        public PausableTaskCompletionSource(TimeSpan delay, bool startPaused)
            : base(delay, startPaused)
        {
        }
    }
}
