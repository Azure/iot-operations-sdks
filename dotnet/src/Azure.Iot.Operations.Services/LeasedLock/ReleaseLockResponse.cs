namespace Azure.Iot.Operations.Services.LeasedLock
{
    /// <summary>
    /// The fields returned by the service in response to a release lock request.
    /// </summary>
    public class ReleaseLockResponse
    {
        /// <summary>
        /// True if the lock was successfully released and false otherwise.
        /// </summary>
        public bool Success { get; internal set; }

        internal ReleaseLockResponse(bool success)
        {
            Success = success;
        }
    }
}