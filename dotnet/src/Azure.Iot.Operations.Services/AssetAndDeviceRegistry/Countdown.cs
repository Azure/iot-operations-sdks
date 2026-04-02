// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using System.Diagnostics;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry
{
    /// <summary>
    /// A countdown timer that performs some async action upon reaching 0 and then restarts itself. This countdown timer can also be
    /// restarted manually at any time.
    /// </summary>
    internal class Countdown : IDisposable
    {
        private Task? _task;
        private CancellationTokenSource? _resetCountdownCancellationToken;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _isRunning;

        private readonly TimeSpan _delay;
        private readonly Func<CancellationToken, Task> _afterDelay;

        internal Countdown(TimeSpan delay, Func<CancellationToken, Task> afterDelay)
        {
            _delay = delay;
            _afterDelay = afterDelay;
        }

        /// <summary>
        /// Start or restart the countdown timer
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for starting the countdown. This token is not checked within the countdown timer.</param>
        internal async Task StartAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_resetCountdownCancellationToken != null)
                {
                    try
                    {
                        _resetCountdownCancellationToken.Cancel();
                        _resetCountdownCancellationToken.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // the cancellation token was already cancelled and disposed, so ignore this error
                    }
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
                            catch (Exception e)
                            {
                                Trace.TraceError("Failed to perform some periodic async task", e);
                            }
                        }
                    }
                    catch (Exception e) when (e is ObjectDisposedException || e is OperationCanceledException)
                    {
                        // The countdown was stopped or disposed, so gracefully end this task without trying to finish the periodic async task
                        return;
                    }
                });
                _isRunning = true;
                _task.Start();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        internal bool IsRunning()
        {
            return _isRunning;
        }

        internal async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_resetCountdownCancellationToken != null)
                {
                    _resetCountdownCancellationToken.Cancel();
                    _resetCountdownCancellationToken.Dispose();
                }
                _isRunning = false;
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
