using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public class AssetChangedEventArgs : EventArgs
    {
        public ChangeType ChangeType { get; set; }

        public string AssetName { get; set; }

        public Asset? Asset { get; set; }

        public AssetChangedEventArgs(string assetName, ChangeType changeType, Asset? asset)
        {
            AssetName = assetName;
            ChangeType = changeType;
            Asset = asset;
        }
    }
}
