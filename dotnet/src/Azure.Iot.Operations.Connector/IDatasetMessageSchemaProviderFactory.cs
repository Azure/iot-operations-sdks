// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public interface IDatasetMessageSchemaProviderFactory
    {
        public IDatasetMessageSchemaProvider CreateDatasetMessageSchemaProvider(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset);
    }
}
