// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record UpdateAssetStatusRequest
{
    public required string AssetName { get; set; }
    public List<DatasetsSchemaSchemaElementSchema>? DatasetsSchema { get; set; }
    public List<Error>? Errors { get; set; }
    public List<EventsSchemaSchemaElementSchema>? EventsSchema { get; set; }
    public int? Version { get; set; }
}
