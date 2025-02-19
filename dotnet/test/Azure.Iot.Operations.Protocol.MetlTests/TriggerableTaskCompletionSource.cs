// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System.Threading.Tasks;

    public class TriggerableTaskCompletionSource : ITriggerable
    {
        private readonly TaskCompletionSource _tcs;

        public Task Task { get => _tcs.Task; }

        public TriggerableTaskCompletionSource()
        {
            _tcs = new TaskCompletionSource();
        }

        public void Trigger()
        {
            _tcs.SetResult();
        }
    }
}
