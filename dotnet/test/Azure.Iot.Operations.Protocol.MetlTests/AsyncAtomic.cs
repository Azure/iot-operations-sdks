namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class AsyncAtomicInt : IDisposable
    {
        private readonly SemaphoreSlim semaphore;
        private int value;

        public AsyncAtomicInt(int initialValue)
        {
            semaphore = new SemaphoreSlim(1);
            value = initialValue;
        }

        public async Task<int> Increment()
        {
            await semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                value++;
                return value;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<int> Read()
        {
            await semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                return value;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public void Dispose()
        {
            semaphore.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
