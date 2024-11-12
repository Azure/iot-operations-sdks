namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System.Threading;

    public class TriggerableCancellationTokenSource : ITriggerable, IDisposable
    {
        private readonly CancellationTokenSource cts;

        public CancellationTokenSource TokenSource { get => cts; }

        public TriggerableCancellationTokenSource()
        {
            cts = new CancellationTokenSource();
        }

        public void Trigger()
        {
            try
            {
                cts.Cancel();
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            cts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
