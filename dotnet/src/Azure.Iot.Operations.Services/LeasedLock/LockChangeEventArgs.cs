// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.LeasedLock
{
    public sealed class LockChangeEventArgs : EventArgs
    {
        /// <summary>
        /// The new state of the lock.
        /// </summary>
        public LockState NewState { get; internal set; }

        /// <summary>
        /// The new holder of the lock. This value is null if this update is that the lock was released.
        /// </summary>
        public LeasedLockHolder? NewLockHolder { get; internal set; }

        /// <summary>
        /// The timestamp associated with this event.
        /// </summary>
        public HybridLogicalClock Timestamp { get; internal set; }

        internal LockChangeEventArgs(LockState newState, HybridLogicalClock timestamp)
        {
            NewState = newState;
            Timestamp = timestamp;
        }
    }
}
