// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class Pausable<T> : IDisposable
        where T : ITriggerable, new()
    {
        private static readonly double MinTickMilliseconds = 16;

        private readonly SemaphoreSlim _semaphore;
        private readonly System.Timers.Timer _timer;

        private TimerState _timerState;
        private TimeSpan _remainingDelay;  // stale unless timerState == TimerState.Paused
        private DateTime _startTime;       // invalid unless timerState == TimerState.Running

        protected readonly T _source;

        public Pausable(TimeSpan delay, bool startPaused)
        {
            _semaphore = new SemaphoreSlim(1);
            _source = new T();

            _timer = new System.Timers.Timer { Interval = delay.TotalMilliseconds, AutoReset = false, Enabled = !startPaused };
            _timer.Elapsed += this.TimerElapsed;

            _timerState = startPaused ? TimerState.Paused : TimerState.Running;
            _remainingDelay = delay;
            _startTime = DateTime.UtcNow;
        }

        public bool HasFired { get => _timerState == TimerState.Fired; }

        public async Task<bool> TryPauseAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (_timerState == TimerState.Paused)
                {
                    throw new InvalidOperationException("TryPause() called when already paused");
                }
                else if (_timerState == TimerState.Fired)
                {
                    return false;
                }

                DateTime now = DateTime.UtcNow;
                _remainingDelay -= now - _startTime;

                if (_remainingDelay <= TimeSpan.Zero)
                {
                    _timerState = TimerState.Fired;
                    _source.Trigger();
                    return false;
                }

                _timerState = TimerState.Paused;
                _timer.Stop();

                return true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task ResumeAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (_timerState == TimerState.Running)
                {
                    throw new InvalidOperationException("Resume() called when not paused");
                }
                else if (_timerState == TimerState.Fired)
                {
                    return;
                }

                _startTime = DateTime.UtcNow;
                _timerState = TimerState.Running;

                _timer.Interval = Math.Max(_remainingDelay.TotalMilliseconds, MinTickMilliseconds);
                _timer.Start();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void TimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            _semaphore.Wait();

            if (_timerState == TimerState.Running)
            {
                _timerState = TimerState.Fired;
                _timer.Enabled = false;
                _source.Trigger();
            }

            _semaphore.Release();
        }

        public void Dispose()
        {
            _semaphore.Dispose();
            GC.SuppressFinalize(this);
        }

        private enum TimerState
        {
            Running,
            Paused,
            Fired
        }
    }
}
