// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DetailsSchemaElement
{
    public string? Code { get; set; }

    public string? CorrelationId { get; set; }

    public string? Info { get; set; }

    public string? Message { get; set; }
}
