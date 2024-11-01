using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public class AssetEndpointProfileChangedEventArgs : EventArgs
    {
        public ChangeType ChangeType { get; set; }

        public AssetEndpointProfile? AssetEndpointProfile { get; set; }

        public AssetEndpointProfileChangedEventArgs(ChangeType changeType, AssetEndpointProfile? assetEndpointProfile)
        {
            ChangeType = changeType;
            AssetEndpointProfile = assetEndpointProfile;
        }
    }
}
