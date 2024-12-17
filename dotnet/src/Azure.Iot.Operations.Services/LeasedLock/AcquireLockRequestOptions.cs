// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.LeasedLock
{
    /// <summary>
    /// Optional fields for an acquire lock request.
    /// </summary>
    public class AcquireLockRequestOptions
    {
        /// <summary>
        /// The optional value to include in the lock's value. If not provided, the lock's value will equal
        /// the lock holder name. If it is provided, the lock's value will equal {holderName}:{sessionId}
        /// </summary>
        /// <remarks>
        /// <para>
        /// If this value is provided, then you'll need to provide the same value to <see cref="ReleaseLockRequestOptions.SessionId"/>
        /// or attempts to release the lock will fail.
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