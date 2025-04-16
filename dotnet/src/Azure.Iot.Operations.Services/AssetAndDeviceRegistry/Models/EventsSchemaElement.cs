// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record EventsSchemaElement
{
    public MessageSchemaReference? MessageSchemaReference { get; set; }

    public required string Name { get; set; }
}
