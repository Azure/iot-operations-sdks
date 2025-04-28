// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Data;
using System.Diagnostics;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetDatasetSchemaElement
{
    public List<AssetDatasetDataPointSchemaElement>? DataPoints { get; set; }

    public Dictionary<string, AssetDatasetDataPointSchemaElement>? DataPointsDictionary
    {
        get
        {
            Dictionary<string, AssetDatasetDataPointSchemaElement>? dictionary = null;
            if (DataPoints != null)
            {
                dictionary = new();
                foreach (AssetDatasetDataPointSchemaElement datapoint in DataPoints)
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

    public List<AssetDatasetDestinationSchemaElement>? Destinations { get; set; }

    public required string Name { get; set; }

    public string? TypeRef { get; set; }
}
