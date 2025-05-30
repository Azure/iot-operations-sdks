// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AkriServiceError
{
    public required string Code { get; set; }

    public required string Message { get; set; }

    public DateTime Timestamp { get; set; }
}
