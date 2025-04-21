// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    internal class MockDatasetSampler : IDatasetSampler
    {
        private readonly bool _isFaulty;
        private int _sampleAttemptCount = 0;

        public MockDatasetSampler(bool isFaulty = false) 
        {
            _isFaulty = isFaulty;
        }

        public ValueTask DisposeAsync()
        {
            // nothing to dispose
            return ValueTask.CompletedTask;
        }

        public Task<TimeSpan> GetSamplingIntervalAsync(AssetDatasetSchemaElement dataset, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TimeSpan.FromMilliseconds(10));
        }

        public Task<byte[]> SampleDatasetAsync(AssetDatasetSchemaElement dataset, CancellationToken cancellationToken = default)
        {
            _sampleAttemptCount++;

            // When faulty, make the first few attempts fail. The connector should continue to try sampling
            // the data and eventually recover.
            if (_isFaulty && _sampleAttemptCount < 10)
            {
                throw new Exception("Some mock exception was encountered while sampling the dataset");
            }

            return Task.FromResult(Encoding.UTF8.GetBytes("someData"));
        }
    }
}
