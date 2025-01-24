// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector
{
    public class SampleableAsset
    {
        public Asset Asset { get; internal set; }

        private Func<Asset, string, CancellationToken, Task> _datasetSamplingFunction { get; set; }

        internal SampleableAsset(Asset asset, Func<Asset, string, CancellationToken, Task> datasetSamplingFunction)
        {
            Asset = asset;
            _datasetSamplingFunction = datasetSamplingFunction;
        }

        public async Task SampleDatasetAsync(string datasetName, CancellationToken cancellationToken = default)
        { 
            await _datasetSamplingFunction.Invoke(Asset, datasetName, cancellationToken);
        }
    }
}
