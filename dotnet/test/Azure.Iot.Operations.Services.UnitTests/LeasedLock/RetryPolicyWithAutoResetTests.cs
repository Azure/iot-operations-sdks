// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Retry;
using Azure.Iot.Operations.Services.LeasedLock;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.LeasedLock;

public class RetryPolicyWithAutoResetTests
{
    [Fact]
    public async Task ShouldRetry_ResetsRetryCounterAfterExpirationInterval()
    {
        // Arrange
        var expirationInterval = TimeSpan.FromMilliseconds(100);

        var retryPolicy = new ExponentialBackoffRetryPolicy(3, 1, TimeSpan.FromMilliseconds(500), false);

        var retryPolicyWithAutoReset = new RetryPolicyWithAutoReset(retryPolicy, expirationInterval);

        // Act
        retryPolicyWithAutoReset.ShouldRetry(null, out var firstDelay);
        await Task.Delay(expirationInterval * 2);
        retryPolicyWithAutoReset.ShouldRetry(null, out var secondDelay);

        // Assert
        Assert.Equal(firstDelay, secondDelay);
    }

    [Fact]
    public void ShouldRetry_DoesNotResetRetryCounterBeforeExpirationInterval()
    {
        // Arrange
        var expirationInterval = TimeSpan.FromMilliseconds(100);

        var retryPolicy = new ExponentialBackoffRetryPolicy(3, 1, TimeSpan.FromMilliseconds(500), false);

        var retryPolicyWithAutoReset = new RetryPolicyWithAutoReset(retryPolicy, expirationInterval);

        // Act
        retryPolicyWithAutoReset.ShouldRetry(null, out var firstDelay);
        retryPolicyWithAutoReset.ShouldRetry(null, out var secondDelay);

        // Assert
        Assert.True(firstDelay < secondDelay);
    }
}
