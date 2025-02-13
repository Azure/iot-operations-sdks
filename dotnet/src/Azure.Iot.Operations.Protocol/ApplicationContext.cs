using System;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol
{
    /// <summary>
    /// Application-wide context containing shared resources like the HybridLogicalClock.
    /// There should only be one instance per application, shared across all sessions.
    /// </summary>
    public class ApplicationContext : IAsyncDisposable
    {
        private static readonly TimeSpan DefaultMaxClockDrift = TimeSpan.FromMinutes(1);
        private readonly SemaphoreSlim _semaphoreHlc = new SemaphoreSlim(1, 1);
        private bool _disposed;

        /// <summary>
        /// The HybridLogicalClock used by the application.
        /// </summary>
        private HybridLogicalClock _applicationHlc;

        /// <summary>
        /// The maximum clock drift allowed for HLC validations.
        /// </summary>
        public TimeSpan MaxClockDrift { get; }

        /// <summary>
        /// Creates a new ApplicationContext with the specified maximum clock drift.
        /// </summary>
        /// <param name="maxClockDrift">Maximum allowed clock drift. Defaults to 1 minute if not specified.</param>
        public ApplicationContext(TimeSpan? maxClockDrift = null)
        {
            MaxClockDrift = maxClockDrift ?? DefaultMaxClockDrift;
            _applicationHlc = new HybridLogicalClock();
        }

        /// <summary>
        /// Updates the application's HybridLogicalClock based on the current time and returns its string representation.
        /// </summary>
        /// <returns>String representation of the updated HLC.</returns>
        /// <exception cref="AkriMqttException">If the update fails due to clock drift or counter overflow.</exception>
        /// <exception cref="ObjectDisposedException">If this instance has been disposed.</exception>
        public async Task<string> UpdateNowHlcAsync()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await _semaphoreHlc.WaitAsync();
            try
            {
                _applicationHlc.Update(maxClockDrift: MaxClockDrift);
                return _applicationHlc.EncodeToString();
            }
            finally
            {
                _semaphoreHlc.Release();
            }
        }

        /// <summary>
        /// Updates the application's HybridLogicalClock based on another HybridLogicalClock.
        /// </summary>
        /// <param name="other">The other HLC to update against.</param>
        /// <exception cref="AkriMqttException">If the update fails due to clock drift or counter overflow.</exception>
        /// <exception cref="ObjectDisposedException">If this instance has been disposed.</exception>
        public async Task UpdateHlcWithOtherAsync(HybridLogicalClock other)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await _semaphoreHlc.WaitAsync();
            try
            {
                _applicationHlc.Update(other, MaxClockDrift);
            }
            finally
            {
                _semaphoreHlc.Release();
            }
        }

        /// <summary>
        /// Gets a snapshot of the current HybridLogicalClock.
        /// </summary>
        /// <returns>A copy of the current HLC.</returns>
        /// <exception cref="ObjectDisposedException">If this instance has been disposed.</exception>
        public async Task<HybridLogicalClock> ReadHlcAsync()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await _semaphoreHlc.WaitAsync();
            try
            {
                return new HybridLogicalClock(_applicationHlc);
            }
            finally
            {
                _semaphoreHlc.Release();
            }
        }
        /// <summary>
        /// Asynchronously releases the resources used by the ApplicationContext.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (!_disposed)
            {
                if (_semaphoreHlc != null)
                {
                    _semaphoreHlc.Dispose();
                }
                _disposed = true;
            }

            await ValueTask.CompletedTask;
        }
    }
}