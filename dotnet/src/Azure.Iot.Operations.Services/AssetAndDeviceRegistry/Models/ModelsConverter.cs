// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.DeviceDiscoveryService;
using Azure.Iot.Operations.Services.StateStore;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

internal static class ModelsConverter
{

    private static JsonDocumentOptions _jsonDocumentOptions = new()
    {
        AllowTrailingCommas = true,
    };

    internal static AssetStatus ToModel(this AdrBaseService.AssetStatus source)
    {
        return new AssetStatus
        {
            Config = source.Config?.ToModel(),
            Datasets = source.Datasets?.Select(x => x.ToModel()).ToList(),
            Events = source.Events?.Select(x => x.ToModel()).ToList(),
            ManagementGroups = source.ManagementGroups?.Select(x => x.ToModel()).ToList(),
            Streams = source.Streams?.Select(x => x.ToModel()).ToList()
        };
    }
    
}
