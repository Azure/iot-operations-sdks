// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    public interface IStateStoreSetResponse : IStateStoreResponse
    {
        /// <summary>
        /// True if the set request executed successfully. False otherwise.
        /// </summary>
        bool Success { get; }
    }
}
