// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record Error
{
    public int? Code { get; set; }
    public string? Message { get; set; }
}
