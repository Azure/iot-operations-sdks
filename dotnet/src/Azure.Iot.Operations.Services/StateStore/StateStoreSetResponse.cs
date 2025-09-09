// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    public class StateStoreSetResponse : IStateStoreSetResponse
    {
        /// <inheritdoc/>
        public bool Success { get; }

        /// <inheritdoc/>
        public HybridLogicalClock? Version { get; set; }

        internal StateStoreSetResponse(HybridLogicalClock version, bool success)
        {
            Version = version;
            Success = success;
        }
    }
}
