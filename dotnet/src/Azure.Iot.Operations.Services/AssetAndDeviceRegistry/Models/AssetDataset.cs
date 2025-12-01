// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Data;
using System.Diagnostics;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetDataset
{
    public List<AssetDatasetDataPoint>? DataPoints { get; set; }

    public Dictionary<string, AssetDatasetDataPoint>? DataPointsDictionary
    {
        get
        {
            Dictionary<string, AssetDatasetDataPoint>? dictionary = null;
            if (DataPoints != null)
            {
                dictionary = new();
                foreach (AssetDatasetDataPoint datapoint in DataPoints)
                {
                    if (!string.IsNullOrWhiteSpace(datapoint.Name))
                    {
                        dictionary[datapoint.Name] = datapoint;
                    }
                    else
                    {
                        Trace.TraceWarning($"Unexpected datapoint with null or empty name found.");
                    }
                }
            }

            return dictionary;
        }
    }

    public string? DataSource { get; set; }

    public List<DatasetDestination>? Destinations { get; set; }

    public required string Name { get; set; }

    public string? TypeRef { get; set; }

    public string? DatasetConfiguration { get; set; }
}
