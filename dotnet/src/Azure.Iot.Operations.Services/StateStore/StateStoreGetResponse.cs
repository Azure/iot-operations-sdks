// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    public class StateStoreGetResponse : IStateStoreGetResponse
    {
        /// <inheritdoc/>
        public StateStoreValue? Value { get; }

        /// <inheritdoc/>
        public HybridLogicalClock? Version { get; set; }

        internal StateStoreGetResponse(HybridLogicalClock? version, StateStoreValue? value)
        {
            Version = version;
            Value = value;
        }
    }
}
