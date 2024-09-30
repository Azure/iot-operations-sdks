using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.LeasedLock
{
    /// <summary>
    /// The fields returned by the service in response to an acquire lock request.
    /// </summary>
    public class AcquireLockResponse
    {
        /// <summary>
        /// True if the lock was successfully acquired and false otherwise.
        /// </summary>
        public bool Success { get; internal set; }

        /// <summary>
        /// The fencing token returned by the service if the lock was successfully acquired. If the lock
        /// was not successfully acquired, then this value will be null.
        /// </summary>
        public HybridLogicalClock? FencingToken { get; internal set; }

        internal AcquireLockResponse(HybridLogicalClock? version, bool result)
        {
            Success = result;
            
            if (Success)
            {
                FencingToken = version;
            }
        }
    }
}