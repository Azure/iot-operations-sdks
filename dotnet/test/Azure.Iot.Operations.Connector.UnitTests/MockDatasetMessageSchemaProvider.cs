// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.UnitTests
{
    internal class MockDatasetMessageSchemaProvider : IDatasetMessageSchemaProvider
    {
        public Task<DatasetMessageSchema?> GetMessageSchemaAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult((DatasetMessageSchema?) null);
        }
    }
}
