// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEventSchemaElement
{
    public JsonDocument? EventConfiguration { get; set; }

    public required string EventNotifier { get; set; }

    public required string Name { get; set; }

    public List<AssetEventDataPointSchemaElement>? DataPoints { get; set; }

    public List<AssetEventDestinationSchemaElement>? Destinations { get; set; }
}
