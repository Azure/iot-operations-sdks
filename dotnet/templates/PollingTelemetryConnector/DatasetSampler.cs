// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;

namespace ConnectorApp
{
    internal class DatasetSampler : IDatasetSampler
    {
        public Task<byte[]> SampleDatasetAsync(Dataset dataset, CancellationToken cancellationToken = default)
        {
            // This method should define a single read attempt of the provided dataset and should return the serialized MQTT payload
            // that the connector will send to the MQTT broker for you.
            //
            // If you have multiple assets or multiple datasets per asset, then you may want to write multiple implementations of this
            // method.
            throw new NotImplementedException();
        }

        public Task<DatasetMessageSchema?> GetMessageSchemaAsync(Dataset dataset, CancellationToken cancellationToken = default)
        {
            // By returning null, no message schema will be registered for telemetry sent for this dataset.
            return Task.FromResult((DatasetMessageSchema?) null);
        }

        public ValueTask DisposeAsync()
        {
            // Nothing to dispose yet
            return ValueTask.CompletedTask;
        }
    }
}
