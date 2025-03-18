// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace EventDrivenTcpThermostatConnector
{
    public class AssetEventReceivedEventArgs : EventArgs
    {
        public byte[] Data { get; set; }
        
        public string AssetName { get; set; }

        public string DatasetName { get; set; }

        public AssetEventReceivedEventArgs(byte[] data, string assetName, string datasetName)
        {
            Data = data;
            AssetName = assetName;
            DatasetName = datasetName;
        }
    }
}
