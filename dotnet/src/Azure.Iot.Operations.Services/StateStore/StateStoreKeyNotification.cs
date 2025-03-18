// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    internal class StateStoreKeyNotification
    {
        internal StateStoreKeyNotification(StateStoreKey key, KeyState keyState, StateStoreValue? value, HybridLogicalClock timestamp)
        {
            Key = key;
            KeyState = keyState;
            Value = value;
            Timestamp = timestamp;
        }

        internal StateStoreKey Key { get; }

        internal KeyState KeyState { get; }

        internal StateStoreValue? Value { get; }

        internal HybridLogicalClock Timestamp { get; }
    }
}
