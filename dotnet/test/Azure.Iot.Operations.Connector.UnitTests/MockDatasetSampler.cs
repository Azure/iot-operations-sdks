using Azure.Iot.Operations.Services.Assets;
using Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    internal class MockDatasetSampler : IDatasetSampler
    {
        private bool _isFaulty;
        private int _sampleAttemptCount = 0;

        public MockDatasetSampler(bool isFaulty = false) 
        { 
        
        }

        public ValueTask DisposeAsync()
        {
            // nothing to dispose
            return ValueTask.CompletedTask;
        }

        public Task<byte[]> SampleDatasetAsync(Dataset dataset, CancellationToken cancellationToken = default)
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

        Task<DatasetMessageSchema?> IDatasetSampler.GetMessageSchemaAsync(Dataset dataset, CancellationToken cancellationToken)
        {
            return Task.FromResult((DatasetMessageSchema?)null);
        }
    }
}
