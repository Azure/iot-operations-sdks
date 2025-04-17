// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEventSchemaElement
{
    public string? EventConfiguration { get; set; }

    public required string EventNotifier { get; set; }

    public required string Name { get; set; }

    public List<AssetEventDataPointSchemaElement>? DataPoints { get; set; }

    public List<AssetEventDestinationSchemaElement>? Destinations { get; set; }
}
