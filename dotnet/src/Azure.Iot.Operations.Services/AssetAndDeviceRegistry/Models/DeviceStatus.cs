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
}
