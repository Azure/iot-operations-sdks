// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Retry
{
    /// <summary>
    /// Implements Binary Exponential Backoff (BEB) retry policy as follows:
    /// CurrentExponent = min(MaxExponent, baseExponent + currentRetryCount)
    /// RetryDelay = min(pow(2, CurrentExponent), maxWait) milliseconds
    /// </summary>
    public class ExponentialBackoffRetryPolicy : IRetryPolicy
    {
        // Default base exponent set equal 6, in this case first retry starts at 2^(6+1)=128 milliseconds, and exceed 1 second delay on retry #4.
        private readonly uint _baseExponent = 6u;

        // Avoid integer overflow (max of 32) and clamp max delay.
        private const uint MaxExponent = 32u;

        /// <summary>
        /// The maximum number of retries
        /// </summary>
        private readonly uint _maxRetries;

        private readonly TimeSpan _maxDelay;
        private readonly bool _useJitter;

        /// <summary>
        /// Creates an instance of this class with a default base exponent equals 6.
        /// </summary>
        /// <param name="maxRetries">The maximum number of retry attempts.</param>
        /// <param name="maxWait">The maximum amount of time to wait between retries.</param>
        /// <param name="useJitter">Whether to add a small, random adjustment to the retry delay to avoid synchronicity in clients retrying.</param>
        public ExponentialBackoffRetryPolicy(uint maxRetries, TimeSpan maxWait, bool useJitter = true)
        {
            _maxRetries = maxRetries;
            _maxDelay = maxWait;
            _useJitter = useJitter;
        }

        /// <summary>
        /// Creates an instance of this class.
        /// </summary>
        /// <param name="maxRetries">The maximum number of retry attempts.</param>
        /// <param name="baseExponent">The base exponent to start the backoff calculation (CurrentExponent(currentRetryCount) = baseExponent + currentRetryCount).</param>
        /// <param name="maxWait">The maximum amount of time to wait between retries.</param>
        /// <param name="useJitter">Whether to add a small, random adjustment to the retry delay to avoid synchronicity in clients retrying.</param>
        public ExponentialBackoffRetryPolicy(uint maxRetries, uint baseExponent, TimeSpan maxWait, bool useJitter = true) :
        this(maxRetries, maxWait, useJitter)
        {
            _baseExponent = baseExponent;
        }

        /// <inheritdoc/>
        public bool ShouldRetry(uint currentRetryCount, Exception? lastException, out TimeSpan retryDelay)
        {
            if (_maxRetries == 0 || currentRetryCount > _maxRetries)
            {
                retryDelay = TimeSpan.Zero;
                return false;
            }

            // Avoid integer overflow and clamp max delay.
            // if currentRetryCount is very high, adding MinExponent would just wrap around safely
            // and decrease the value suddenly, so exponent gets capped before addition
            // Result: The delay stays at maximum instead of suddenly dropping.
            uint exponent;
            if (currentRetryCount > uint.MaxValue - _baseExponent)
            {
                exponent = MaxExponent;
            }
            else
            {
                exponent = currentRetryCount + _baseExponent;
                exponent = Math.Min(MaxExponent, exponent);
            }

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
        private TimeSpan UpdateWithJitter(double baseTimeMs)
        {
            // Don't calculate jitter if the value is very small
            if (baseTimeMs < 50)
            {
                return TimeSpan.FromMilliseconds(baseTimeMs);
            }

            double jitterMs = Random.Shared.Next(95, 106) * baseTimeMs / 100.0;

            return TimeSpan.FromMilliseconds(jitterMs);
        }
    }
}
