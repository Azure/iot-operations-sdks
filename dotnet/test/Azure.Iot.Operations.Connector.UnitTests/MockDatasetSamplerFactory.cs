using Azure.Iot.Operations.Services.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    internal class MockDatasetSamplerFactory : IDatasetSamplerFactory
    {
        private bool _isFaulty;
        public MockDatasetSamplerFactory(bool isFaulty = false)
        {
            _isFaulty = isFaulty;
        }

        public IDatasetSampler CreateDatasetSampler(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset)
        {
            return new MockDatasetSampler(_isFaulty);
        }
    }
}
