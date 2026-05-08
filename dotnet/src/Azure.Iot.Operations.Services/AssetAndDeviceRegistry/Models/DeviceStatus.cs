// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceStatus
{
    /// <summary>
    /// The status of the device
    /// </summary>
    public ConfigStatus? Config { get; set; }

    /// <summary>
    /// The statuses of each of the endpoints that belong to the device
    /// </summary>
    public DeviceStatusEndpoint? Endpoints { get; set; }

    public void SetEndpointError(string inboundEndpointName, ConfigError endpointError)
    {
        Endpoints ??= new();
        Endpoints.Inbound ??= new();
        Endpoints.Inbound[inboundEndpointName] ??= new();
        Endpoints.Inbound[inboundEndpointName].Error = endpointError;
    }

    /// <summary>
    /// Compare two device states.
    /// </summary>
    /// <param name="other">The other device state to compare against.</param>
    /// <returns>False if there is any difference between the two device states (ignoring 'LastUpdateTime' field values). True otherwise.</returns>
    /// <remarks>
    /// The 'LastUpdateTime' fields are deliberately ignored in this comparison.
    /// </remarks>
    public bool EqualTo(DeviceStatus other)
    {
        if (Config == null && other.Config != null)
        {
            return false;
        }
        else if (Config != null && other.Config == null)
        {
            return false;
        }
        else if (Config != null && other.Config != null && !Config.EqualTo(other.Config))
        {
            return false;
        }

        if (Endpoints == null && other.Endpoints != null)
        {
            return false;
        }
        else if (Endpoints != null && other.Endpoints == null)
        {
            return false;
        }
        else if (Endpoints != null && other.Endpoints != null && !Endpoints.EqualTo(other.Endpoints))
        {
            return false;
        }

        return true;
    }
}
