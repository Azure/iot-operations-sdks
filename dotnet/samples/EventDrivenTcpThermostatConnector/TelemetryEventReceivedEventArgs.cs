// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;

namespace EventDrivenRestThermostatConnector
{
    public class TelemetryEventReceivedEventArgs : EventArgs
    {
        public byte[] Data { get; set; }
        
        public string AssetName { get; set; }

        public string DatasetName { get; set; }

        public TelemetryEventReceivedEventArgs(byte[] data, string assetName, string datasetName)
        {
            Data = data;
            AssetName = assetName;
            DatasetName = datasetName;
        }
    }
}
