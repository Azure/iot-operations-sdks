namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    using System;
    using System.Threading;

    public class PausableCancellationTokenSource : Pausable<TriggerableCancellationTokenSource>, IPausable
    {
        public CancellationTokenSource TokenSource { get => source.TokenSource; }

        public PausableCancellationTokenSource(TimeSpan delay, bool startPaused)
            : base(delay, startPaused)
        {
        }
    }
}
