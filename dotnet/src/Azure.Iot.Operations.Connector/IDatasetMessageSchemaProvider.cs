// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public interface IDatasetMessageSchemaProvider
    {
        public Task<DatasetMessageSchema?> GetMessageSchemaAsync(CancellationToken cancellationToken = default);
    }
}
