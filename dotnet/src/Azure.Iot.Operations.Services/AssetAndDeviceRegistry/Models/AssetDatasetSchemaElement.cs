// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Data;
using System.Diagnostics;
using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetDatasetSchemaElement
{
    public List<AssetDataPointSchemaElement>? DataPoints { get; set; } = default;

    public Dictionary<string, AssetDataPointSchemaElement>? DataPointsDictionary
    {
        get
        {
            Dictionary<string, AssetDataPointSchemaElement>? dictionary = null;
            if (DataPoints != null)
            {
                dictionary = new();
                foreach (AssetDataPointSchemaElement datapoint in DataPoints)
                {
                    if (!string.IsNullOrWhiteSpace(datapoint.Name))
                    {
                        dictionary[datapoint.Name] = datapoint;
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

    public JsonDocument? DatasetConfiguration { get; set; } = default;

    public string? Name { get; set; } = default;

    public Topic? Topic { get; set; } = default;
}
