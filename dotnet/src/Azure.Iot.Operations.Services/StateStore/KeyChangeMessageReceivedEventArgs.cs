// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    public sealed class KeyChangeMessageReceivedEventArgs : EventArgs
    {
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
        /// The timestamp attached to the key change event.
        /// </summary>
        public HybridLogicalClock Timestamp { get; internal set; }

        internal KeyChangeMessageReceivedEventArgs(StateStoreKey changedKey, KeyState newState, HybridLogicalClock timestamp)
        {
            ChangedKey = changedKey;
            NewState = newState;
            Timestamp = timestamp;
        }
    }
}
