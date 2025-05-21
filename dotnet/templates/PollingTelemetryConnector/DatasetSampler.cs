// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace PollingTelemetryConnectorTemplate
{
    internal class DatasetSampler : IDatasetSampler
    {
        public Task<TimeSpan> GetSamplingIntervalAsync(AssetDatasetSchemaElement dataset, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> SampleDatasetAsync(AssetDatasetSchemaElement dataset, CancellationToken cancellationToken = default)
        {
            // This method should define a single read attempt of the provided dataset and should return the serialized MQTT payload
            // that the connector will send to the MQTT broker for you.
            //
            // If you have multiple assets or multiple datasets per asset, then you may want to write multiple implementations of this
            // method.
            throw new NotImplementedException();
        }
    }
}
