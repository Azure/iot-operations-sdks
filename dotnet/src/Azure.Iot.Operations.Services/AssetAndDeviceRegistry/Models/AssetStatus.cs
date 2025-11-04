// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetStatus
{
    public ConfigStatus? Config { get; set; }

    public List<AssetDatasetEventStreamStatus>? Datasets { get; set; }

    public List<AssetEventGroupStatus>? EventGroups { get; set; } = default;

    public List<AssetManagementGroupStatusSchemaElement>? ManagementGroups { get; set; }

    public List<AssetDatasetEventStreamStatus>? Streams { get; set; }
}
