// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DefaultDatasetsDestinationsSchemaElement
{
    public required DestinationConfiguration Configuration { get; set; }
    public DatasetTarget Target { get; set; }
}
