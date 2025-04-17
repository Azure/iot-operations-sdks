// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEndpointProfileStatus
{
    public List<Error>? Errors { get; set; }
}
