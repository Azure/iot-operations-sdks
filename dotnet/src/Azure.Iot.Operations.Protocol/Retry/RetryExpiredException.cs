// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Retry
{
    /// <summary>
    /// This exception is thrown when an operation is cancelled because the configured retry 
    /// policy dictated that the operation shouldn't retry any longer.
    /// </summary>
    public class RetryExpiredException : Exception
    {
        public RetryExpiredException()
        {
        }

        public RetryExpiredException(string? message) : base(message)
        {
        }

        public RetryExpiredException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
