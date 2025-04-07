// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record GetAssetRequest
{
    public required string AssetName { get; init; }
}
