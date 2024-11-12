namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System.Threading;
    using System.Threading.Tasks;

    public class AsyncCountdownEvent
    {
        private readonly SemaphoreSlim mutexSemaphore;
        private readonly SemaphoreSlim waitSemaphore;
        private readonly int maxWaiters;
        private int count;

        public AsyncCountdownEvent(int initialCount, int maxWaiters)
        {
            if (initialCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCount));
            }

            mutexSemaphore = new SemaphoreSlim(1);
            waitSemaphore = new SemaphoreSlim(0);
            this.maxWaiters = maxWaiters;
            count = initialCount;
        }

        public async Task WaitAsync(CancellationToken cancellationToken = default)
        {
            await waitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task WaitAsync(TimeSpan timeout)
        {
            await waitSemaphore.WaitAsync(timeout).ConfigureAwait(false);
        }

        public async Task SignalAsync()
        {
            await mutexSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                count--;
                if (count <= 0)
                {
                    waitSemaphore.Release(maxWaiters);
                }
            }
            finally
            {
                mutexSemaphore.Release();
            }
        }
    }
}
