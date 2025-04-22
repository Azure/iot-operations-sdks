// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DetectedAssetEventSchemaElement
{
    public string? EventConfiguration { get; set; }

    public required string EventNotifier { get; set; }

    public string? LastUpdatedOn { get; set; }

    public required string Name { get; set; }

    public Topic? Topic { get; set; }
}
