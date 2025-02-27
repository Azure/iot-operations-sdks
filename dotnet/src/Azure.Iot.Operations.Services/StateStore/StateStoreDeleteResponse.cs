// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.StateStore
{
    public class StateStoreDeleteResponse
    {
        /// <summary>
        /// The number of items deleted by the request.
        /// </summary>
        /// <remarks>
        /// If this delete operation was conditional on <see cref="StateStoreDeleteRequestOptions.OnlyDeleteIfValueEquals"/>
        /// and the request was not carried out because of that condition, then this value will be -1.
        /// </remarks>
        public int? DeletedItemsCount { get; internal set; }

        internal StateStoreDeleteResponse(int? deletedItemsCount = null)
        {
            DeletedItemsCount = deletedItemsCount;
        }
    }
}
