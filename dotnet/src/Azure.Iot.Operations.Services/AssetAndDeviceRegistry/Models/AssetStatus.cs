// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetStatus
{
    public AssetStatusConfig? Config { get; set; }

    public List<AssetStatusDatasetSchemaElement>? Datasets { get; set; }

    public List<EventsSchemaElement>? Events { get; set; }

    public List<AssetStatusManagementGroupSchemaElement>? ManagementGroups { get; set; }

    public List<AssetStatusStreamSchemaElement>? Streams { get; set; }
}
