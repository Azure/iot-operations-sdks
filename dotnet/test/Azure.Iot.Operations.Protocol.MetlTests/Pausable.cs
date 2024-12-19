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

        private readonly SemaphoreSlim semaphore;
        private readonly System.Timers.Timer timer;

        private TimerState timerState;
        private TimeSpan remainingDelay;  // stale unless timerState == TimerState.Paused
        private DateTime startTime;       // invalid unless timerState == TimerState.Running

        protected readonly T source;

        public Pausable(TimeSpan delay, bool startPaused)
        {
            semaphore = new SemaphoreSlim(1);
            source = new T();

            timer = new System.Timers.Timer { Interval = delay.TotalMilliseconds, AutoReset = false, Enabled = !startPaused };
            timer.Elapsed += this.TimerElapsed;

            timerState = startPaused ? TimerState.Paused : TimerState.Running;
            remainingDelay = delay;
            startTime = DateTime.UtcNow;
        }

        public bool HasFired { get => timerState == TimerState.Fired; }

        public async Task<bool> TryPauseAsync()
        {
            await semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (timerState == TimerState.Paused)
                {
                    throw new InvalidOperationException("TryPause() called when already paused");
                }
                else if (timerState == TimerState.Fired)
                {
                    return false;
                }

                DateTime now = DateTime.UtcNow;
                remainingDelay -= now - startTime;

                if (remainingDelay <= TimeSpan.Zero)
                {
                    timerState = TimerState.Fired;
                    source.Trigger();
                    return false;
                }

                timerState = TimerState.Paused;
                timer.Stop();

                return true;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task ResumeAsync()
        {
            await semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (timerState == TimerState.Running)
                {
                    throw new InvalidOperationException("Resume() called when not paused");
                }
                else if (timerState == TimerState.Fired)
                {
                    return;
                }

                startTime = DateTime.UtcNow;
                timerState = TimerState.Running;

                timer.Interval = Math.Max(remainingDelay.TotalMilliseconds, MinTickMilliseconds);
                timer.Start();
            }
            finally
            {
                semaphore.Release();
            }
        }

        private void TimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            semaphore.Wait();

            if (timerState == TimerState.Running)
            {
                timerState = TimerState.Fired;
                timer.Enabled = false;
                source.Trigger();
            }

            semaphore.Release();
        }

        public void Dispose()
        {
            semaphore.Dispose();
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
