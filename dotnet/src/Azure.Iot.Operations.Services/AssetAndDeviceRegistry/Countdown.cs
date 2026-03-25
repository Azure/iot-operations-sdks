// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry
{
    internal class Countdown : IDisposable
    {
        private Task? _task;
        private CancellationTokenSource? _resetCountdownCancellationToken;
        private readonly SemaphoreSlim _semaphore = new(1, 1);


        private readonly TimeSpan _delay;
        private readonly Func<CancellationToken, Task> _afterDelay;

        internal Countdown(TimeSpan delay, Func<CancellationToken, Task> afterDelay)
        {
            _delay = delay;
            _afterDelay = afterDelay;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_resetCountdownCancellationToken != null)
                {
                    _resetCountdownCancellationToken.Cancel();
                    _resetCountdownCancellationToken.Dispose();
                }

                _resetCountdownCancellationToken = new CancellationTokenSource();

                _task = new Task(async () =>
                {
                    try
                    {
                        while (true)
                        {
                            await Task.Delay(_delay, _resetCountdownCancellationToken.Token);

                            try
                            {
                                await _afterDelay.Invoke(_resetCountdownCancellationToken.Token);
                            }
                            catch (Exception)
                            {
                                //todo log error
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // countdown was reset
                        return;
                    }
                });

                _task.Start();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_resetCountdownCancellationToken != null)
                {
                    _resetCountdownCancellationToken.Cancel();
                    _resetCountdownCancellationToken.Dispose();
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _resetCountdownCancellationToken?.Cancel();
            _resetCountdownCancellationToken?.Dispose();
            _semaphore.Dispose();
        }
    }
}
