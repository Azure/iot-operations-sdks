// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector
{
    public interface IAdrClientWrapper
    {
        event EventHandler<AssetChangedEventArgs>? AssetChanged;

        event EventHandler<AssetEndpointProfileChangedEventArgs>? AssetEndpointProfileChanged;

        void ObserveAssetEndpointProfiles();

        void ObserveAssets(string aepName);

        void UnobserveAssetEndpointProfiles();

        void UnobserveAssets(string aepName);

        Task UnobserveAllAsync(CancellationToken cancellationToken = default);
    }
}
