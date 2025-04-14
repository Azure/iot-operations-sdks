// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DetailsSchemaElement
{
    public string? Code { get; set; } = default;

    public string? CorrelationId { get; set; } = default;

    public string? Info { get; set; } = default;

    public string? Message { get; set; } = default;
}
