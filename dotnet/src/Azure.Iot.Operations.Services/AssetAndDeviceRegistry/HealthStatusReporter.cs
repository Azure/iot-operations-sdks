// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry
{
    public class HealthStatusReporter
    {
        public static DeviceEndpointHealthStatusReporter CreateDeviceEndpointHealthStatusReporter(
            IAzureDeviceRegistryClient azureDeviceRegistryClient,
            string deviceName,
            string inboundEndpointName)
        {
            return new DeviceEndpointHealthStatusReporter(azureDeviceRegistryClient, deviceName, inboundEndpointName);
        }
    }
}
