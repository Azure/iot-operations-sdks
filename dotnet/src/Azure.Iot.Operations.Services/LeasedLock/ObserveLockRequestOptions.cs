// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.LeasedLock
{
    public class ObserveLockRequestOptions
    {
        /// <summary>
        /// If true, notifications about this lock changing will include the new holder of the lock after the change.
        /// If false, notifications about this lock changing will not include the new holder.
        /// </summary>
        /// <remarks>
        /// The new value will be set in <see cref="LockChangeEventArgs.NewLockHolder"/>
        /// </remarks>
        public bool GetNewValue { get; set; } = false;
    }
}
