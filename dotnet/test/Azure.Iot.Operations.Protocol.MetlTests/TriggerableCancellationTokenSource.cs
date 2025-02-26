// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System.Threading;

    public class TriggerableCancellationTokenSource : ITriggerable, IDisposable
    {
        private readonly CancellationTokenSource _cts;

        public CancellationTokenSource TokenSource { get => _cts; }

        public TriggerableCancellationTokenSource()
        {
            _cts = new CancellationTokenSource();
        }

        public void Trigger()
        {
            try
            {
                _cts.Cancel();
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            _cts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
