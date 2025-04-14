// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record ConfigError
{
    public string? Code { get; set; } = default;

    public List<DetailsSchemaElement>? Details { get; set; } = default;

    public Dictionary<string, string>? InnerError { get; set; } = default;

    public string? Message { get; set; } = default;
}
