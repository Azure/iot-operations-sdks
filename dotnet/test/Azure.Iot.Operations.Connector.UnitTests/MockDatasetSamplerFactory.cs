// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Files;
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

        public IDatasetSampler CreateDatasetSampler(string deviceName, Device device, string inboundEndpointName, string assetName, Asset asset, AssetDataset dataset, EndpointCredentials? deviceCredentials)
        {
            return new MockDatasetSampler(_isFaulty);
        }
    }
}
