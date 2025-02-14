// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public interface IDatasetSampler
    {
        Task<byte[]> SampleDatasetAsync(Dataset dataset, CancellationToken cancellationToken = default);
    }
}
