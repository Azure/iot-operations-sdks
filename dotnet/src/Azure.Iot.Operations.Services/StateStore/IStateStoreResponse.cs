// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    public interface IStateStoreResponse
    {
        /// <summary>
        /// The version of the key returned by the service, if applicable.
        /// </summary>
        /// <remarks>
        /// In cases where no key is returned by the service (such as when getting a key
        /// that isn't present in the store), this value is null.
        /// </remarks>
        HybridLogicalClock? Version { get; set; }
    }
}
