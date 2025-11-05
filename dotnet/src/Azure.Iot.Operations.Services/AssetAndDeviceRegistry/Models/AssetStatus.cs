// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetStatus
{
    /// <summary>
    /// The status of the asset
    /// </summary>
    /// <remarks>
    /// This status is independent from the status of any nested event groups/datasets/streams. That means that,
    /// even if a dataset has a config error, the asset status may still be okay.
    /// </remarks>
    public ConfigStatus? Config { get; set; }

    /// <summary>
    /// The status of all datasets associated with this asset (if it has any datasets).
    /// </summary>
    public List<AssetDatasetEventStreamStatus>? Datasets { get; set; }

    /// <summary>
    /// The status of all event groups associated with this asset (if it has any event groups).
    /// </summary>
    public List<AssetEventGroupStatus>? EventGroups { get; set; } = default;

    /// <summary>
    /// The status of all management groups associated with this asset (if it has any management groups).
    /// </summary>

    public List<AssetManagementGroupStatusSchemaElement>? ManagementGroups { get; set; }

    /// <summary>
    /// The status of all streams associated with this asset (if it has any streams).
    /// </summary>
    public List<AssetDatasetEventStreamStatus>? Streams { get; set; }
}
