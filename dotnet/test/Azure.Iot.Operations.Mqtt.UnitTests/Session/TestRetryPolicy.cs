// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Retry;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class TestRetryPolicy : IRetryPolicy
    {
        private readonly int _maxRetryCount;
        private readonly TimeSpan _retryDelay;

        public int CurrentRetryCount { get; private set; }

        public TestRetryPolicy(int maxRetryCount)
        { 
            _maxRetryCount = maxRetryCount;
            _retryDelay = TimeSpan.Zero;
        }

        public TestRetryPolicy(int maxRetryCount, TimeSpan retryDelay)
        {
            _maxRetryCount = maxRetryCount;
            _retryDelay = retryDelay;
        }

        public bool ShouldRetry(uint currentRetryCount, Exception lastException, out TimeSpan retryDelay)
        {
            CurrentRetryCount++;
            retryDelay = _retryDelay;
            return currentRetryCount <= _maxRetryCount;
        }
    }
}
