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
        /// The holder of the lock before this change. This value is null if this update is that the lock had no previous holder.
        /// </summary>
        public LeasedLockHolder? PreviousLockHolder { get; internal set; }

        internal LockChangeEventArgs(LockState newState)
        {
            NewState = newState;
        }
    }
}
