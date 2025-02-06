// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;

namespace SqlQualityAnalyzerConnectorApp
{
    internal class DatasetMessageSchemaProvider : IDatasetMessageSchemaProvider
    {
        public Task<DatasetMessageSchema?> GetMessageSchemaAsync(CancellationToken cancellationToken = default)
        {
            // By returning null, no message schema will be registered for telemetry sent for this dataset.
            return Task.FromResult((DatasetMessageSchema?)null);
        }
    }
}
