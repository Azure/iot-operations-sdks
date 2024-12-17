// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    /// <summary>
    /// The optional parameters for a Delete request.
    /// </summary>
    public class StateStoreDeleteRequestOptions
    {
        /// <summary>
        /// If provided, the delete operation will only execute if the value in the State Store matches this value.
        /// If not provided, the delete operation will execute unconditionally.
        /// </summary>
        public StateStoreValue? OnlyDeleteIfValueEquals { get; set; } = null;

        /// <summary>
        /// The optional fencing token to include in the request.
        /// </summary>
        public HybridLogicalClock? FencingToken { get; set; }
    }
}