using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    public sealed class KeyChangeMessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// The version of the key returned by the service.
        /// </summary>
        public HybridLogicalClock? Version { get; set; }

        /// <summary>
        /// The key that was created, updated, or deleted.
        /// </summary>
        public StateStoreKey ChangedKey { get; internal set; }

        /// <summary>
        /// The new state of the key.
        /// </summary>
        public KeyState NewState { get; internal set; }

        /// <summary>
        /// The new value of the key. This value is null if this update is that the key was deleted.
        /// </summary>
        public StateStoreValue? NewValue { get; internal set; }

        /// <summary>
        /// The value of the key before this change. This value is null if this update is that the key was created.
        /// </summary>
        public StateStoreValue? PreviousValue { get; internal set; }

        internal KeyChangeMessageReceivedEventArgs(HybridLogicalClock? version, StateStoreKey changedKey, KeyState newState)
        {
            Version = version;
            ChangedKey = changedKey;
            NewState = newState;
        }
    }
}
