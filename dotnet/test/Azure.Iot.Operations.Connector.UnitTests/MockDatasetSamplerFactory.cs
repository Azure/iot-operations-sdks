// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Assets;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    internal class MockDatasetSamplerFactory : IDatasetSamplerFactory
    {
        private readonly bool _isFaulty;

        public MockDatasetSamplerFactory(bool isFaulty = false)
        {
            _isFaulty = isFaulty;
        }

        public IDatasetSampler CreateDatasetSampler(Device device, string inboundEndpointName, Asset asset, AssetDatasetSchemaElement dataset, EndpointCredentials? deviceCredentials)
        {
            return new MockDatasetSampler(_isFaulty);
        }
    }
}
