// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class AsyncAtomicInt : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private int _value;

        public AsyncAtomicInt(int initialValue)
        {
            _semaphore = new SemaphoreSlim(1);
            _value = initialValue;
        }

        public async Task<int> IncrementAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                _value++;
                return _value;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<int> ReadAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                return _value;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _semaphore.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
