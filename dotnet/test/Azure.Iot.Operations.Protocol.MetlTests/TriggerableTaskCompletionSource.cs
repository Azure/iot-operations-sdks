// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System.Threading.Tasks;

    public class TriggerableTaskCompletionSource : ITriggerable
    {
        private readonly TaskCompletionSource tcs;

        public Task Task { get => tcs.Task; }

        public TriggerableTaskCompletionSource()
        {
            tcs = new TaskCompletionSource();
        }

        public void Trigger()
        {
            tcs.SetResult();
        }
    }
}
