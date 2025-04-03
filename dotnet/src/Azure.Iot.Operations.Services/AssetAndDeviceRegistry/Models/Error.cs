// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record Error
{
    public required int Code { get; set; }
    public required string Message { get; set; }
}
