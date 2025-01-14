// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.LeasedLock
{
    /// <summary>
    /// The state of the leased lock.
    /// </summary>
    public enum LockState
    {
        /// <summary>
        /// The lock was just acquired.
        /// </summary>
        Acquired,

        /// <summary>
        /// The lock was released, or the previous lease on the lock expired. The lock is available to acquire
        /// at this time.
        /// </summary>
        Released,
    }
}