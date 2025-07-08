// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Retry;

namespace Azure.Iot.Operations.Protocol.UnitTests.Retry;

public class ExponentialBackoffRetryPolicyTests
{
    [Fact]
    public void ShouldRetry_ReturnsTrueOnFirstRetry()
    {
        // Arrange
        var retryPolicy = new ExponentialBackoffRetryPolicy(10, TimeSpan.FromMilliseconds(200), false);

        // Act
        bool shouldRetry = retryPolicy.ShouldRetry(1, new Exception(), out TimeSpan retryDelay);

        // Assert
        Assert.True(shouldRetry);
        Assert.Equal(TimeSpan.FromMilliseconds(128), retryDelay);
    }

    [Fact]
    public void ShouldRetry_ReturnsFalseWhenMaxRetriesExceeded()
    {
        // Arrange
        var retryPolicy = new ExponentialBackoffRetryPolicy(3, TimeSpan.FromMilliseconds(200), false);

        // Act
        bool shouldRetry = retryPolicy.ShouldRetry(4, new Exception(), out TimeSpan retryDelay);

        // Assert
        Assert.False(shouldRetry);
        Assert.Equal(TimeSpan.Zero, retryDelay);
    }

    [Fact]
    public void ShouldRetry_UsesJitterWhenEnabled()
    {
        // Arrange
        var retryPolicy = new ExponentialBackoffRetryPolicy(10, TimeSpan.FromMilliseconds(200), true);

        // Act
        bool shouldRetry = retryPolicy.ShouldRetry(1, new Exception(), out TimeSpan retryDelay);

        // Assert
        Assert.True(shouldRetry);
        Assert.InRange(retryDelay.TotalMilliseconds, 121.6, 134.4); // 128ms ± 5%
    }

    [Fact]
    public void ShouldRetry_ClampsToMaxDelay()
    {
        // Arrange
        var retryPolicy = new ExponentialBackoffRetryPolicy(10, TimeSpan.FromMilliseconds(150), false);

        // Act
        bool shouldRetry = retryPolicy.ShouldRetry(10, new Exception(), out TimeSpan retryDelay);

        // Assert
        Assert.True(shouldRetry);
        Assert.Equal(TimeSpan.FromMilliseconds(150), retryDelay);
    }

    [Fact]
    public void ShouldRetry_UsesBaseExponentForFirstRetry()
    {
        // Arrange
        var retryPolicy = new ExponentialBackoffRetryPolicy(10, 1, TimeSpan.FromMilliseconds(200), false);

        // Act
        bool shouldRetry = retryPolicy.ShouldRetry(1, new Exception(), out TimeSpan retryDelay);

        // Assert
        Assert.True(shouldRetry);
        Assert.Equal(TimeSpan.FromMilliseconds(4), retryDelay);
    }
}
