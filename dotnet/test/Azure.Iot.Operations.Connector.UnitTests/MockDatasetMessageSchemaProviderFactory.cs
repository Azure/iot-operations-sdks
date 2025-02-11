// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    internal class MockDatasetMessageSchemaProviderFactory : IMessageSchemaProviderFactory
    {
        public IMessageSchemaProvider CreateMessageSchemaProvider(AssetEndpointProfile assetEndpointProfile, Asset asset)
        {
            return new MockMessageSchemaProvider();
        }
    }
}
