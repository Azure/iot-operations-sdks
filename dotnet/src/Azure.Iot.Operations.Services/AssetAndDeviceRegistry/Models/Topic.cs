// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record Topic
{
    public required string Path { get; set; }

    public Retain? Retain { get; set; }
}
