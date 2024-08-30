namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
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
