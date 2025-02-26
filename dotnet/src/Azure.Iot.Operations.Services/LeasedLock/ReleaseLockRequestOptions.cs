// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.LeasedLock
{
    /// <summary>
    /// Optional fields for a release lock request.
    /// </summary>
    public class ReleaseLockRequestOptions
    {
        /// <summary>
        /// If true, this operation will also stop any auto-renewing configured by <see cref="LeasedLockClient.AutomaticRenewalOptions"/>.
        /// If false, any auto-renewing will continue as-is.
        /// </summary>
        /// <remarks>
        /// By default, auto-renewal will be cancelled.
        /// </remarks>
        public bool CancelAutomaticRenewal { get; set; } = true;

        /// <summary>
        /// The optional value to include in the lock's value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Only provide this value if the sessionId was set when acquiring the lock in <see cref="AcquireLockRequestOptions.SessionId"/>.
        /// If the sessionId was set when acquiring the lock, but not when releasing the lock (or vice versa), then
        /// attempts to release the lock will fail.
        /// </para>
        /// <para>
        /// By providing a unique sessionId, an application can use the same holderName and/or the same MQTT client
        /// in different threads to acquire the same lock without worrying about accidentally allowing two clients
        /// to both own a lock at the same time.
        /// </para>
        /// </remarks>
        public string? SessionId { get; set; }
    }
}