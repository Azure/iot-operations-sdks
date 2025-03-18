// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.LeasedLock
{
    public class GetLockHolderResponse
    {
        /// <summary>
        /// The current lock holder.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value is null if no one is currently holding the lock.
        /// </para>
        /// <para>
        /// A lock holder's value is set when a <see cref="LeasedLockClient"/> is constructed.
        /// </para>
        /// </remarks>
        public LeasedLockHolder? LockHolder { get; }

        internal GetLockHolderResponse(LeasedLockHolder? lockHolder)
        {
            LockHolder = lockHolder;
        }
    }
}
