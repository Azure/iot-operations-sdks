﻿using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using GenericHttpServerConnectorWorkerService;
using System.Text.Json;

namespace Azure.Iot.Operations.GenericHttpConnectorSample
{
    // TODO Since the design is for this to handle sampling/serializing for X datasets, I should probably add another dataset to show how that would work.
    public interface IHttpServerDatasetSampler
    {
        //TODO something like this to be more generic
        public Task<byte[]> SampleAsync(Dataset dataset, CancellationToken cancellationToken = default);
    }
}
