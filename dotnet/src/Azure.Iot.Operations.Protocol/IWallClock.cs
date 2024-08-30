namespace Azure.Iot.Operations.Protocol
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IWallClock
    {
        DateTime UtcNow { get; }

        CancellationTokenSource CreateCancellationTokenSource(TimeSpan delay);

        Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout, CancellationToken cancellationToken);
    }
}
