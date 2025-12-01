// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceStatusEndpoint
{
    public Dictionary<string, DeviceStatusInboundEndpointSchemaMapValue>? Inbound { get; set; }

    internal bool EqualTo(DeviceStatusEndpoint other)
    {
        if (Inbound == null && other.Inbound != null)
        {
            return false;
        }
        else if (Inbound != null && other.Inbound == null)
        {
            return false;
        }
        else if (Inbound != null && other.Inbound != null)
        {
            if (Inbound.Count != other.Inbound.Count)
            {
                return false;
            }

            foreach (string key in Inbound.Keys)
            {
                var expectedValue = Inbound[key];
                if (other.Inbound.TryGetValue(key, out var value))
                {
                    if (!expectedValue.EqualTo(value))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            foreach (string key in other.Inbound.Keys)
            {
                var expectedValue = other.Inbound[key];
                if (Inbound.TryGetValue(key, out var value))
                {
                    if (!expectedValue.EqualTo(value))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        return true;
    }
}
