// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEvent
{
    public string? EventConfiguration { get; set; }

    public required string EventNotifier { get; set; }

    public required string Name { get; set; }

    public string? TypeRef { get; set; }

    public List<AssetEventDataPointSchemaElement>? DataPoints { get; set; }

    public List<EventStreamDestination>? Destinations { get; set; }
}
