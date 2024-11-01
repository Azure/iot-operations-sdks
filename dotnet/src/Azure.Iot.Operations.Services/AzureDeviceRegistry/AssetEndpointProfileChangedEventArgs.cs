using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public class AssetEndpointProfileChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Specifies if the change in this asset endpoint profile was that it was updated, deleted, or created
        /// </summary>
        public ChangeType ChangeType { get; set; }

        public AssetEndpointProfile? AssetEndpointProfile { get; set; }

        public AssetEndpointProfileChangedEventArgs(ChangeType changeType, AssetEndpointProfile? assetEndpointProfile)
        {
            ChangeType = changeType;
            AssetEndpointProfile = assetEndpointProfile;
        }
    }
}
