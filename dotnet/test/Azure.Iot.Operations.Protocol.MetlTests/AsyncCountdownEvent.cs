// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System.Threading;
    using System.Threading.Tasks;

    public class AsyncCountdownEvent
    {
        private readonly SemaphoreSlim _mutexSemaphore;
        private readonly SemaphoreSlim _waitSemaphore;
        private readonly int _maxWaiters;
        private int _count;

        public AsyncCountdownEvent(int initialCount, int maxWaiters)
        {
            if (initialCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCount));
            }

            _mutexSemaphore = new SemaphoreSlim(1);
            _waitSemaphore = new SemaphoreSlim(0);
            _maxWaiters = maxWaiters;
            _count = initialCount;
        }

        public async Task WaitAsync(CancellationToken cancellationToken = default)
        {
            await _waitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task WaitAsync(TimeSpan timeout)
        {
            await _waitSemaphore.WaitAsync(timeout).ConfigureAwait(false);
        }

        public async Task SignalAsync()
        {
            await _mutexSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                _count--;
                if (_count <= 0)
                {
                    _waitSemaphore.Release(_maxWaiters);
                }
            }
            finally
            {
                _mutexSemaphore.Release();
            }
        }
    }
}
