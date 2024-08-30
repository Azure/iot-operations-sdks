namespace Azure.Iot.Operations.Protocol
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class WallClock : IWallClock
    {
        public DateTime UtcNow { get => DateTime.UtcNow; }

        public CancellationTokenSource CreateCancellationTokenSource(TimeSpan delay) => new CancellationTokenSource(delay);

        public Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout, CancellationToken cancellationToken) => task.WaitAsync(timeout, cancellationToken);
    }
}
