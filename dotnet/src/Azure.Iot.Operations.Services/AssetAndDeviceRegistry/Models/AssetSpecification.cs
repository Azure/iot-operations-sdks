// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetSpecification
{
    public string? AssetEndpointProfileRef { get; set; } = default;

    public Dictionary<string, string>? Attributes { get; set; } = default;

    public List<AssetDatasetSchemaElement>? Datasets { get; set; } = default;

    public Dictionary<string, AssetDatasetSchemaElement>? DatasetsDictionary
    {
        get
        {
            Dictionary<string, AssetDatasetSchemaElement>? dictionary = null;
            if (Datasets != null)
            {
                dictionary = new();
                foreach (AssetDatasetSchemaElement dataset in Datasets)
                {
                    if (!string.IsNullOrWhiteSpace(dataset.Name))
                    {
                        dictionary[dataset.Name] = dataset;
                    }
                    else
                    {
                        Trace.TraceWarning($"Unexpected dataset with null or empty name found.");
                    }
                }
            }

            return dictionary;
        }
    }

    public JsonDocument? DefaultDatasetsConfiguration { get; set; } = default;

    public JsonDocument? DefaultEventsConfiguration { get; set; } = default;

    public Topic? DefaultTopic { get; set; } = default;

    public string? Description { get; set; } = default;

    public List<string>? DiscoveredAssetRefs { get; set; } = default;

    public string? DisplayName { get; set; } = default;

    public string? DocumentationUri { get; set; } = default;

    public bool? Enabled { get; set; } = default;

    public List<AssetEventSchemaElement>? Events { get; set; } = default;

    public Dictionary<string, AssetEventSchemaElement>? EventsDictionary
    {
        get
        {
            Dictionary<string, AssetEventSchemaElement>? dictionary = null;
            if (Events != null)
            {
                dictionary = new();
                foreach (AssetEventSchemaElement assetEvent in Events)
                {
                    if (!string.IsNullOrWhiteSpace(assetEvent.Name))
                    {
                        dictionary[assetEvent.Name] = assetEvent;
                    }
                    else
                    {
                        Trace.TraceWarning($"Unexpected dataset with null or empty name found.");
                    }
                }
            }

            return dictionary;
        }
    }

    public string? ExternalAssetId { get; set; } = default;

    public string? HardwareRevision { get; set; } = default;

    public string? Manufacturer { get; set; } = default;

    public string? ManufacturerUri { get; set; } = default;

    public string? Model { get; set; } = default;

    public string? ProductCode { get; set; } = default;

    public string? SerialNumber { get; set; } = default;

    public string? SoftwareRevision { get; set; } = default;

    public string? Uuid { get; set; } = default;

    public string? Version { get; set; } = default;
}
