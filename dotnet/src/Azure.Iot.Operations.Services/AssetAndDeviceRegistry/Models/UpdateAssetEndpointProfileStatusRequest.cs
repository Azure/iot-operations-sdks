// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record UpdateAssetEndpointProfileStatusRequest
{
    public required List<Error> Errors { get; set; }
}
