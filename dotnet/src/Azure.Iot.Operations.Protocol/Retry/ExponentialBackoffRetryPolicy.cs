// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Retry
{
    /// <summary>
    /// Creates an instance of this class.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retry attempts</param>
    /// <param name="maxWait">The maximum amount of time to wait between retries.</param>
    /// <param name="useJitter">Whether to add a small, random adjustment to the retry delay to avoid synchronicity in clients retrying.</param>
    public class ExponentialBackoffRetryPolicy(uint maxRetries, TimeSpan maxWait, bool useJitter = true) : IRetryPolicy
    {
        private readonly Random _rng = new();
        private readonly object _rngLock = new();

        // If we start with an exponent of 1 to calculate the number of millisecond delay, it starts too low and takes too long to get over 1 second.
        // So we always add 6 to the retry count to start at 2^7=128 milliseconds, and exceed 1 second delay on retry #4.
        private const uint MinExponent = 6u;

        // Avoid integer overlow (max of 32) and clamp max delay.
        private const uint MaxExponent = 32u;

        /// <summary>
        /// The maximum number of retries
        /// </summary>
        private readonly uint _maxRetries = maxRetries;

        private readonly TimeSpan _maxDelay = maxWait;
        private readonly bool _useJitter = useJitter;

        /// <inheritdoc/>
        public bool ShouldRetry(uint currentRetryCount, Exception lastException, out TimeSpan retryDelay)
        {
            if (_maxRetries == 0 || currentRetryCount > _maxRetries)
            {
                retryDelay = TimeSpan.Zero;
                return false;
            }

            // Avoid integer overlow and clamp max delay.
            uint exponent = currentRetryCount + MinExponent;
            exponent = Math.Min(MaxExponent, exponent);

            // 2 to the power of the retry count gives us exponential back-off.
            double exponentialIntervalMs = Math.Pow(2.0, exponent);

            double clampedWaitMs = Math.Min(exponentialIntervalMs, _maxDelay.TotalMilliseconds);

            retryDelay = _useJitter
                ? UpdateWithJitter(clampedWaitMs)
                : TimeSpan.FromMilliseconds(clampedWaitMs);

            return true;
        }

        /// <summary>
        /// Gets jitter between 95% and 105% of the base time.
        /// </summary>
        protected TimeSpan UpdateWithJitter(double baseTimeMs)
        {
            // Don't calculate jitter if the value is very small
            if (baseTimeMs < 50)
            {
                return TimeSpan.FromMilliseconds(baseTimeMs);
            }

            double jitterMs;

            // Because Random is not threadsafe
            lock (_rngLock)
            {
                // A random double from 95% to 105% of the baseTimeMs
                jitterMs = _rng.Next(95, 106);
            }

            jitterMs *= baseTimeMs / 100.0;

            return TimeSpan.FromMilliseconds(jitterMs);
        }
    }
}
