using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.Assets
{
    public class AssetChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Specifies if the change in this asset was that it was updated, deleted, or created
        /// </summary>
        public ChangeType ChangeType { get; set; }

        public string AssetName { get; set; }

        /// <summary>
        /// The new value of the asset.
        /// </summary>
        /// <remarks>
        /// This value is null if the asset was deleted.
        /// </remarks>
        public Asset? Asset { get; set; }

        public AssetChangedEventArgs(string assetName, ChangeType changeType, Asset? asset)
        {
            AssetName = assetName;
            ChangeType = changeType;
            Asset = asset;
        }
    }
}
