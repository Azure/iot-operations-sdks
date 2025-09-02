// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.StateStore
{
    public class StateStoreDeleteResponse : IStateStoreDeleteResponse
    {
        /// <inheritdoc/>
        public int? DeletedItemsCount { get; }

        internal StateStoreDeleteResponse(int? deletedItemsCount = null)
        {
            DeletedItemsCount = deletedItemsCount;
        }
    }
}
