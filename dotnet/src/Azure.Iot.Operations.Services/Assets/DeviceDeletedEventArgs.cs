// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Assets
{
    public class DeviceDeletedEventArgs : EventArgs
    {
        public string AssetEndpointProfileName { get; set; }

        internal DeviceDeletedEventArgs(string name)
        {
            AssetEndpointProfileName = name;
        }
    }
}
