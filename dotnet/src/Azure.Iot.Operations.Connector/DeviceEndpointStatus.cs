// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    public class DeviceEndpointStatus
    {
        public ConfigStatus? ConfigStatus { get; set; }

        public ConfigError? ConfigError { get; set; }
    }
}
