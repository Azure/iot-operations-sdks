// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.Assets
{
    public class DeviceCreatedEventArgs : EventArgs
    {
        public string AssetEndpointProfileName { get; set; }

        internal DeviceCreatedEventArgs(string name)
        {
            AssetEndpointProfileName = name;
        }
    }
}
