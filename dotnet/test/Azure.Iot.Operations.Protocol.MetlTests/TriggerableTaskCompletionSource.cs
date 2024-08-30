namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
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
