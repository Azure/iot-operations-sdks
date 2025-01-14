// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Retry
{
    /// <summary>
    /// A retry policy that will never retry.
    /// </summary>
    public class NoRetryPolicy : IRetryPolicy
    {
        /// <summary>
        /// Creates a retry policy that will never retry.
        /// </summary>
        public NoRetryPolicy()
        {
        }

        /// <inheritdoc/>
        public bool ShouldRetry(uint currentRetryCount, Exception lastException, out TimeSpan retryDelay)
        {
            retryDelay = TimeSpan.Zero;
            return false;
        }
    }
}
