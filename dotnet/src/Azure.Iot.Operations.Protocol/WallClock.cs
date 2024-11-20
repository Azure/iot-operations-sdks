using System;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol
{
    public class WallClock : IWallClock
    {
        public DateTime UtcNow => DateTime.UtcNow;

        public CancellationTokenSource CreateCancellationTokenSource(TimeSpan delay)
        {
            return new CancellationTokenSource(delay);
        }

        public Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return task.WaitAsync(timeout, cancellationToken);
        }
    }
}
