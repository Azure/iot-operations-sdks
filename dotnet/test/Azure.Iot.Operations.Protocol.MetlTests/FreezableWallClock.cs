// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System;
    using System.Threading;
    using System.Collections.Generic;

    public class FreezableWallClock : IWallClock, IDisposable
    {
        private readonly SemaphoreSlim _mutexSemaphore;
        private int _nextTicket;
        private readonly HashSet<int> _activeTickets;
        private bool _isFrozen;
        private TimeSpan _timeOffset;
        private DateTime _frozenTime;
        private List<IPausable> _pausableSources;

        public DateTime UtcNow
        {
            get
            {
                _mutexSemaphore.Wait();
                var testTime = _isFrozen ? _frozenTime : DateTime.UtcNow + _timeOffset;
                _mutexSemaphore.Release();
                return testTime;
            }
        }

        public FreezableWallClock()
        {
            _mutexSemaphore = new SemaphoreSlim(1);
            _nextTicket = 0;
            _activeTickets = new HashSet<int>();
            _isFrozen = false;
            _timeOffset = TimeSpan.Zero;
            _frozenTime = DateTime.UtcNow;
            _pausableSources = new List<IPausable>();
        }

        public CancellationTokenSource CreateCancellationTokenSource(TimeSpan delay)
        {
            _mutexSemaphore.Wait();

            PausableCancellationTokenSource pausableCts;

            try
            {
                pausableCts = new PausableCancellationTokenSource(delay, startPaused: _isFrozen);
                _pausableSources.Add(pausableCts);
            }
            finally
            {
                _mutexSemaphore.Release();
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
            await _mutexSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            PausableTaskCompletionSource pausableTcs;

            try
            {
                pausableTcs = new PausableTaskCompletionSource(duration, startPaused: _isFrozen);
                _pausableSources.Add(pausableTcs);
            }
            finally
            {
                _mutexSemaphore.Release();
            }

            await pausableTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task WaitUntilAsync(DateTime desiredTime, CancellationToken cancellationToken = default)
        {
            return WaitForAsync(desiredTime - UtcNow, cancellationToken);
        }

        public async Task<int> FreezeTimeAsync()
        {
            await _mutexSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (!_isFrozen)
                {
                    _frozenTime = DateTime.UtcNow + _timeOffset;
                    _isFrozen = true;

                    foreach (IPausable source in _pausableSources)
                    {
                        await source.TryPauseAsync().ConfigureAwait(false);
                    }

                    _pausableSources = _pausableSources.Where(p => !p.HasFired).ToList();
                }

                int newTicket = _nextTicket;
                ++_nextTicket;

                if (!_activeTickets.Add(newTicket))
                {
                    throw new Exception($"FreezableWallClock.Freeze(): ticket number {newTicket} already outstanding");
                }

                return newTicket;
            }
            finally
            {
                _mutexSemaphore.Release();
            }
        }

        public async Task UnfreezeTimeAsync(int ticket)
        {
            await _mutexSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (!_isFrozen)
                {
                    throw new Exception($"FreezableWallClock.Unfreeze(): clock already unfrozen");
                }

                if (!_activeTickets.Remove(ticket))
                {
                    throw new Exception($"FreezableWallClock.Unfreeze({ticket}): ticket number not outstanding");
                }

                if (_activeTickets.Count == 0)
                {
                    _timeOffset = _frozenTime - DateTime.UtcNow;
                    _isFrozen = false;

                    foreach (IPausable source in _pausableSources)
                    {
                        await source.ResumeAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _mutexSemaphore.Release();
            }
        }

        public void Dispose()
        {
            _mutexSemaphore.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
