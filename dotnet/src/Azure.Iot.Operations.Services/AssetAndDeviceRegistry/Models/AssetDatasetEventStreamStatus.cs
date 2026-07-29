// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetDatasetEventStreamStatus
{
    public ConfigError? Error { get; set; }
    public MessageSchemaReference? MessageSchemaReference { get; set; }

    public required string Name { get; set; }

    internal bool EqualTo(AssetDatasetEventStreamStatus other)
    {
        if (!string.Equals(Name, other.Name))
        {
            return false;
        }

        if (Error == null && other.Error != null)
        {
            return false;
        }
        else if (Error != null && other.Error == null)
        {
            return false;
        }
        else if (Error != null && other.Error != null && !Error.EqualTo(other.Error))
        {
            return false;
        }

        if (MessageSchemaReference == null && other.MessageSchemaReference != null)
        {
            return false;
        }
        else if (MessageSchemaReference != null && other.MessageSchemaReference == null)
        {
            return false;
        }
        else if (MessageSchemaReference != null && other.MessageSchemaReference != null && !MessageSchemaReference.EqualTo(other.MessageSchemaReference))
        {
            return false;
        }

        return true;
    }
}
