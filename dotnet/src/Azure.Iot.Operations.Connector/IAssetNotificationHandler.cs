// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public interface IAssetNotificationHandler
    {
        public Task OnAssetSampleable(SampleableAsset sampleableAsset);

        public Task OnAssetNotSampleable(string assetName);
    }
}
