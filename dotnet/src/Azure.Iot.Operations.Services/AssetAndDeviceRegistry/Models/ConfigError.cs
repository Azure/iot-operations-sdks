// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record ConfigError
{
    public string? Code { get; set; }

    public List<DetailsSchemaElement>? Details { get; set; }

    public Dictionary<string, string>? InnerError { get; set; }

    public string? Message { get; set; }
}
