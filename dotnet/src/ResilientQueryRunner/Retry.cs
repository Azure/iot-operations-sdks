// <copyright file="Retry.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace ResilientQueryRunner;

/// <summary>
/// Classifies exceptions as retryable or not.
/// </summary>
public interface IFailureClassifier
{
    /// <summary>
    /// Determines if the exception is retryable.
    /// </summary>
    bool IsRetryable(Exception exception);
}

/// <summary>
/// Default implementation of failure classification.
/// </summary>
public sealed class DefaultFailureClassifier : IFailureClassifier
{
    /// <summary>
    /// Determines if the exception is retryable.
    /// </summary>
    public bool IsRetryable(Exception exception)
        => exception is not (OperationCanceledException or ArgumentException or InvalidOperationException);
}

/// <summary>
/// Strategy for computing retry times.
/// </summary>
public interface IRetryStrategy
{
    /// <summary>
    /// Computes the next retry time.
    /// </summary>
    DateTimeOffset ComputeNextRetryUtc(DateTimeOffset nowUtc, int consecutiveFailureCount);
}

/// <summary>
/// Exponential backoff retry strategy.
/// </summary>
public sealed class ExponentialBackoffRetryStrategy : IRetryStrategy
{
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maxDelay;

    /// <summary>
    /// Initializes a new instance of the strategy.
    /// </summary>
    public ExponentialBackoffRetryStrategy(TimeSpan initialDelay, TimeSpan maxDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDelay, initialDelay);

        _initialDelay = initialDelay;
        _maxDelay = maxDelay;
    }

    /// <summary>
    /// Computes the next retry time using exponential backoff.
    /// </summary>
    public DateTimeOffset ComputeNextRetryUtc(DateTimeOffset nowUtc, int consecutiveFailureCount)
    {
        var exp = Math.Min(consecutiveFailureCount, 16);
        var delayMs = _initialDelay.TotalMilliseconds * Math.Pow(2, exp - 1);
        var delay = TimeSpan.FromMilliseconds(Math.Min(delayMs, _maxDelay.TotalMilliseconds));
        return nowUtc + delay;
    }
}
