// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DiscoveredAsset
{
    public List<string>? AssetTypeRefs { get; set; }

    public Dictionary<string, string>? Attributes { get; set; }


    public string? AssetName { get; set; }

    public List<DiscoveredAssetDataset>? Datasets { get; set; }

    public string? DefaultDatasetsConfiguration { get; set; }

    public List<DatasetDestination>? DefaultDatasetsDestinations { get; set; }

    public string? DefaultEventsConfiguration { get; set; }

    public List<EventStreamDestination>? DefaultEventsDestinations { get; set; }

    public string? DefaultManagementGroupsConfiguration { get; set; }

    public string? DefaultStreamsConfiguration { get; set; }

    public List<EventStreamDestination>? DefaultStreamsDestinations { get; set; }

    public required AssetDeviceRef DeviceRef { get; set; }

    public string? DocumentationUri { get; set; }

    public List<DetectedAssetEventSchemaElement>? Events { get; set; }

    public string? HardwareRevision { get; set; }

    public List<DiscoveredAssetManagementGroup>? ManagementGroups { get; set; }
    public string? Manufacturer { get; set; }

    public string? ManufacturerUri { get; set; }

    public string? Model { get; set; }

    public string? ProductCode { get; set; }

    public string? SerialNumber { get; set; }

    public string? SoftwareRevision { get; set; }

    public List<DiscoveredAssetStream>? Streams { get; set; }
}

public class DiscoveredAssetStream
{
    public List<EventStreamDestination>? Destinations { get; set; } = default;

    public DateTime? LastUpdatedOn { get; set; } = default;

    public string Name { get; set; } = default!;

    public string? StreamConfiguration { get; set; } = default;

    public string? TypeRef { get; set; } = default;
}

public class DiscoveredAssetManagementGroup
{
    public List<DiscoveredAssetManagementGroupAction>? Actions { get; set; } = default;

    public uint? DefaultTimeOutInSeconds { get; set; } = default;

    public string? DefaultTopic { get; set; } = default;

    public DateTime? LastUpdatedOn { get; set; } = default;

    public string? ManagementGroupConfiguration { get; set; } = default;

    public string Name { get; set; } = default!;

    public string? TypeRef { get; set; } = default;
}

public class DiscoveredAssetManagementGroupAction
{
    public string? ActionConfiguration { get; set; } = default;

    public AssetManagementGroupActionType ActionType { get; set; } = default!;

    public DateTime? LastUpdatedOn { get; set; } = default;

    public string Name { get; set; } = default!;

    public string TargetUri { get; set; } = default!;

    public uint? TimeOutInSeconds { get; set; } = default;

    public string? Topic { get; set; } = default;

    public string? TypeRef { get; set; } = default;
}
