// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public class MockAdrClientWrapper : IAdrClientWrapper
    {
        public event EventHandler<AssetChangedEventArgs>? AssetChanged;
        public event EventHandler<AssetEndpointProfileChangedEventArgs>? AssetEndpointProfileChanged;

        public void ObserveAssetEndpointProfiles()
        {
            throw new NotImplementedException();
        }

        public void ObserveAssets(string aepName)
        {
            throw new NotImplementedException();
        }

        public Task UnobserveAllAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public void UnobserveAssetEndpointProfiles()
        {
            throw new NotImplementedException();
        }

        public void UnobserveAssets(string aepName)
        {
            throw new NotImplementedException();
        }
    }
}
