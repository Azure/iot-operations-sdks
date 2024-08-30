using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.StateStore
{
    internal class StateStoreKeyNotification
    {
        internal StateStoreKeyNotification(StateStoreKey key, KeyState keyState, StateStoreValue? value)
        {
            Key = key;
            KeyState = keyState;
            Value = value;
        }

        internal StateStoreKey Key { get; }

        internal KeyState KeyState { get; }

        internal StateStoreValue? Value { get; }
    }
}
