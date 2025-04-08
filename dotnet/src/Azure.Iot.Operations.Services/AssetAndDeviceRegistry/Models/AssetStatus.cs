// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetStatus
{
    public List<DatasetsSchemaElement>? DatasetsSchema { get; set; }

    public List<Error>? Errors { get; set; }

    public List<EventsSchemaElement>? EventsSchema { get; set; }

    public int? Version { get; set; }
}
