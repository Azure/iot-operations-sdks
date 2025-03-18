// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Retry;

namespace Azure.Iot.Operations.Services.LeasedLock;

internal class RetryPolicyWithAutoReset
{
    private readonly IRetryPolicy _retryPolicy;
    private readonly TimeSpan _expirationInterval;
    private DateTime _lastRetryTime = DateTime.MaxValue;
    private uint _currentRetryCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryPolicyWithAutoReset"/> class.
    /// </summary>
    /// <param name="retryPolicy">The retry policy to be used.</param>
    /// <param name="expirationInterval">The time interval since last call of the ShouldRetry after which it will reset policy retry counter
    /// before next call of the <paramref name="retryPolicy"/>.ShouldRetry.</param>
    public RetryPolicyWithAutoReset(IRetryPolicy retryPolicy, TimeSpan expirationInterval)
    {
        _retryPolicy = retryPolicy;
        _expirationInterval = expirationInterval;
        _currentRetryCount = 0;
    }

    /// <summary>
    /// Determines whether the operation should be retried and returns the time interval to wait before the next retry.
    /// </summary>
    /// <param name="lastException">The exception that caused the operation to fail.</param>
    /// <param name="retryDelay">The time interval to wait before the next retry.</param>
    /// <returns>True if the operation should be retried; otherwise, false.</returns>
    public bool ShouldRetry(Exception? lastException, out TimeSpan retryDelay)
    {
        var shouldResetCounter = DateTime.UtcNow - _lastRetryTime > _expirationInterval;
        _lastRetryTime = DateTime.UtcNow;

        if (shouldResetCounter)
        {
            Reset();
        }
        else
        {
            _currentRetryCount++;
        }

        return _retryPolicy.ShouldRetry(_currentRetryCount, lastException, out retryDelay);
    }

    public void Reset()
    {
        _currentRetryCount = 1;
    }
}
