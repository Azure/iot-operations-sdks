using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Retry
{
    public interface IRetryPolicy
    {
        /// <summary>
        /// Method called by the client when an operation fails to determine if a retry should be attempted,
        /// and how long to wait until retrying the operation.
        /// </summary>
        /// <param name="currentRetryCount">The number of times the current operation has been attempted.</param>
        /// <param name="lastException">The exception that prompted this retry policy check.</param>
        /// <param name="retryDelay">Set this to the desired time to delay before the next attempt.</param>
        /// <returns>True if the operation should be retried; otherwise false.</returns>
        /// <example>
        /// <code language="csharp">
        /// class CustomRetryPolicy : IRetryPolicy
        /// {
        ///     public bool ShouldRetry(uint currentRetryCount, Exception lastException, out TimeSpan retryDelay)
        ///     {
        ///         // Add custom logic as needed upon determining if it should retry and set the retryDelay out parameter
        ///     }
        /// }
        /// </code>
        /// </example>
        bool ShouldRetry(uint currentRetryCount, Exception lastException, out TimeSpan retryDelay);
    }
}
