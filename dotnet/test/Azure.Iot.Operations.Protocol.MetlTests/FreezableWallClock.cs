namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    using System;
    using System.Threading;
    using System.Collections.Generic;

    public class FreezableWallClock : IWallClock, IDisposable
    {
        private SemaphoreSlim mutexSemaphore;
        private int nextTicket;
        private HashSet<int> activeTickets;
        private bool isFrozen;
        private TimeSpan timeOffset;
        private DateTime frozenTime;
        private List<IPausable> pausableSources;

        public DateTime UtcNow
        {
            get
            {
                mutexSemaphore.Wait();
                var testTime = isFrozen ? frozenTime : DateTime.UtcNow + timeOffset;
                mutexSemaphore.Release();
                return testTime;
            }
        }

        public FreezableWallClock()
        {
            mutexSemaphore = new SemaphoreSlim(1);
            nextTicket = 0;
            activeTickets = new HashSet<int>();
            isFrozen = false;
            timeOffset = TimeSpan.Zero;
            frozenTime = DateTime.UtcNow;
            pausableSources = new List<IPausable>();
        }

        public CancellationTokenSource CreateCancellationTokenSource(TimeSpan delay)
        {
            mutexSemaphore.Wait();

            PausableCancellationTokenSource pausableCts;

            try
            {
                pausableCts = new PausableCancellationTokenSource(delay, startPaused: isFrozen);
                pausableSources.Add(pausableCts);
            }
            finally
            {
                mutexSemaphore.Release();
            }

            return pausableCts.TokenSource;
        }

        public async Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            CancellationTokenSource timeoutCts = CreateCancellationTokenSource(timeout);
            CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            try
            {
                return await task.WaitAsync(linkedCts.Token);
            }
            catch (Exception ex)
            {
                throw timeoutCts.Token.IsCancellationRequested ? new TimeoutException() :
                    cancellationToken.IsCancellationRequested ? new OperationCanceledException() :
                    ex;
            }
        }

        public async Task WaitForAsync(TimeSpan duration, CancellationToken cancellationToken = default)
        {
            await mutexSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            PausableTaskCompletionSource pausableTcs;

            try
            {
                pausableTcs = new PausableTaskCompletionSource(duration, startPaused: isFrozen);
                pausableSources.Add(pausableTcs);
            }
            finally
            {
                mutexSemaphore.Release();
            }

            await pausableTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task WaitUntilAsync(DateTime desiredTime, CancellationToken cancellationToken = default)
        {
            return WaitForAsync(desiredTime - this.UtcNow, cancellationToken);
        }

        public async Task<int> FreezeTimeAsync()
        {
            await mutexSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (!isFrozen)
                {
                    frozenTime = DateTime.UtcNow + timeOffset;
                    isFrozen = true;

                    foreach (IPausable source in pausableSources)
                    {
                        await source.TryPauseAsync().ConfigureAwait(false);
                    }

                    pausableSources = pausableSources.Where(p => !p.HasFired).ToList();
                }

                int newTicket = nextTicket;
                ++nextTicket;

                if (!activeTickets.Add(newTicket))
                {
                    throw new Exception($"FreezableWallClock.Freeze(): ticket number {newTicket} already outstanding");
                }

                return newTicket;
            }
            finally
            {
                mutexSemaphore.Release();
            }
        }

        public async Task UnfreezeTimeAsync(int ticket)
        {
            await mutexSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (!isFrozen)
                {
                    throw new Exception($"FreezableWallClock.Unfreeze(): clock already unfrozen");
                }

                if (!activeTickets.Remove(ticket))
                {
                    throw new Exception($"FreezableWallClock.Unfreeze({ticket}): ticket number not outstanding");
                }

                if (activeTickets.Count == 0)
                {
                    timeOffset = frozenTime - DateTime.UtcNow;
                    isFrozen = false;

                    foreach (IPausable source in pausableSources)
                    {
                        await source.ResumeAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                mutexSemaphore.Release();
            }
        }

        public void Dispose()
        {
            mutexSemaphore.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
