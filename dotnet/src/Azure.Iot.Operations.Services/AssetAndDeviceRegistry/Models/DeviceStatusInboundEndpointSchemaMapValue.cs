// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceStatusInboundEndpointSchemaMapValue
{
    public ConfigError? Error { get; set; }

    internal bool EqualTo(DeviceStatusInboundEndpointSchemaMapValue other)
    {
        if (Error == null && other.Error != null)
        {
            return false;
        }
        else if (Error != null && other.Error == null)
        {
            return false;
        }
        else if (Error != null && other.Error != null && !Error.EqualTo(other.Error))
        {
            return false;
        }

        return true;
    }
}
