// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.GenericHttpConnectorSample;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.ConnectorSample
{
    internal class ThermostatStatusHttpServerSampler : IDatasetSampler
    {
        public Task<byte[]> SampleAsync(Dataset dataset, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
