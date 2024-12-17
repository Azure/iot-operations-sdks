// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    public class StateStoreKey : StateStoreObject
    {
        /// <summary>
        /// The version of the key.
        /// </summary>
        /// <remarks>
        /// The State Store service tracks the version of each key and updates it each time it is set. The value
        /// of this version for a particular key is returned in the response for each get or set request via
        /// <see cref="StateStoreResponse.Version"/>.
        /// </remarks>
        /// <remarks>
        /// This value is read-only because you set the version of a key when you call 
        /// <see cref="StateStoreClient.SetAsync(StateStoreKey, StateStoreValue, HybridLogicalClock, StateStoreSetRequestOptions?, CancellationToken)"/>.
        /// </remarks>
        public HybridLogicalClock? Version { get; }

        public StateStoreKey(string key) : base(key)
        {
        }

        public StateStoreKey(byte[] key) : base(key)
        {
        }

        public StateStoreKey(Stream value) : base(value)
        {
        }

        public StateStoreKey(Stream value, long length) : base(value, length)
        {
        }

        public StateStoreKey(ArraySegment<byte> value) : base(value)
        {
        }

        public StateStoreKey(IEnumerable<byte> value) : base(value)
        {
        }

        public static implicit operator StateStoreKey(string value)
        {
            if (value == null || value.Length == 0)
            {
                return new StateStoreKey(string.Empty);
            }

            return new StateStoreKey(value);
        }
    }
}