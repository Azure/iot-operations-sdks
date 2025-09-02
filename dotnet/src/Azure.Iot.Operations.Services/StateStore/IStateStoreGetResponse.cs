// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    public interface IStateStoreGetResponse : IStateStoreResponse
    {
        /// <summary>
        /// The requested value associated with the key.
        /// </summary>
        /// <remarks>
        /// This value is null if the requested key isn't in the State Store.
        /// </remarks>
        StateStoreValue? Value { get; }
    }
}
