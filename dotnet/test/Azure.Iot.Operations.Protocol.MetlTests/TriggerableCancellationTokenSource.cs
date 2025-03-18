// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System.Threading;

    public class TriggerableCancellationTokenSource : ITriggerable, IDisposable
    {
        public CancellationTokenSource TokenSource { get; }

        public TriggerableCancellationTokenSource()
        {
            TokenSource = new CancellationTokenSource();
        }

        public void Trigger()
        {
            try
            {
                TokenSource.Cancel();
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            TokenSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
