// <copyright file="RetryPolicyConfig.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Akri.ConnectorFramework.Authentication;

/// <summary>
/// Configuration for HTTP retry policies with exponential backoff.
/// </summary>
public sealed class RetryPolicyConfig : IValidatableObject
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// Defaults to 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial delay before the first retry.
    /// Subsequent retries use exponential backoff (2x, 4x, etc.).
    /// Defaults to 2 seconds.
    /// </summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the maximum delay between retries.
    /// Prevents exponential backoff from growing too large.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the jitter factor (0.0 to 1.0) added to retry delays.
    /// Helps prevent thundering herd problems.
    /// Defaults to 0.1 (10% jitter).
    /// </summary>
    public double JitterFactor { get; set; } = 0.1;

    /// <summary>
    /// Gets or sets whether to enable circuit breaker.
    /// When enabled, the circuit opens after consecutive failures.
    /// Defaults to true.
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of consecutive failures before opening the circuit.
    /// Defaults to 5.
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the duration the circuit stays open before transitioning to half-open.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validates the retry policy configuration.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MaxRetryAttempts < 0)
        {
            yield return new ValidationResult(
                "MaxRetryAttempts must be non-negative.",
                [nameof(MaxRetryAttempts)]);
        }

        if (MaxRetryAttempts > 10)
        {
            yield return new ValidationResult(
                "MaxRetryAttempts should not exceed 10.",
                [nameof(MaxRetryAttempts)]);
        }

        if (InitialRetryDelay < TimeSpan.Zero)
        {
            yield return new ValidationResult(
                "InitialRetryDelay must be non-negative.",
                [nameof(InitialRetryDelay)]);
        }

        if (MaxRetryDelay < InitialRetryDelay)
        {
            yield return new ValidationResult(
                "MaxRetryDelay must be greater than or equal to InitialRetryDelay.",
                [nameof(MaxRetryDelay)]);
        }

        if (JitterFactor < 0.0 || JitterFactor > 1.0)
        {
            yield return new ValidationResult(
                "JitterFactor must be between 0.0 and 1.0.",
                [nameof(JitterFactor)]);
        }

        if (EnableCircuitBreaker)
        {
            if (CircuitBreakerThreshold <= 0)
            {
                yield return new ValidationResult(
                    "CircuitBreakerThreshold must be positive when circuit breaker is enabled.",
                    [nameof(CircuitBreakerThreshold)]);
            }

            if (CircuitBreakerDuration <= TimeSpan.Zero)
            {
                yield return new ValidationResult(
                    "CircuitBreakerDuration must be positive when circuit breaker is enabled.",
                    [nameof(CircuitBreakerDuration)]);
            }
        }
    }
}
