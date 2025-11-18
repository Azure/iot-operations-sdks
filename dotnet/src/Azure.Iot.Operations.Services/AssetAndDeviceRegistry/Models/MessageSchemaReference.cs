// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record MessageSchemaReference
{
    public required string SchemaName { get; set; }

    public required string SchemaRegistryNamespace { get; set; }

    public required string SchemaVersion { get; set; }

    internal bool EqualTo(MessageSchemaReference other)
    {
        return string.Equals(SchemaName, other.SchemaName)
            && string.Equals(SchemaRegistryNamespace, other.SchemaRegistryNamespace)
            && string.Equals(SchemaVersion, other.SchemaVersion);
    }
}
