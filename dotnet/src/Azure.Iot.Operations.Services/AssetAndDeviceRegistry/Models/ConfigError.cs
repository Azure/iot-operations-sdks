// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record ConfigError
{
    /// <summary>
    /// Error code for classification of errors (ex: '400', '404', '500', etc.).
    /// </summary>
    public string? Code { get; set; } = default;

    /// <summary>
    /// Array of error details that describe the status of each error.
    /// </summary>
    public List<DetailsSchemaElement>? Details { get; set; } = default;

    /// <summary>
    /// Human readable helpful error message to provide additional context for error (ex: “capability Id ''foo'' does not exist”).
    /// </summary>
    public string? Message { get; set; } = default;

    internal bool EqualTo(ConfigError other)
    {
        if (!string.Equals(Code, other.Code))
        {
            return false;
        }

        if (!string.Equals(Message, other.Message))
        {
            return false;
        }

        if (Details == null && other.Details != null)
        {
            return false;
        }
        else if (Details != null && other.Details == null)
        {
            return false;
        }
        else if (Details != null && other.Details != null)
        {
            if (Details.Count != other.Details.Count)
            {
                return false;
            }

            // All detail entries in this are present exactly once in other
            foreach (DetailsSchemaElement detail in Details)
            {
                var matches = other.Details.Select((a) => a.EqualTo(detail));
                if (matches == null || matches.Count() != 1)
                {
                    return false;
                }
            }

            // All detail entries in other are present exactly once in this
            foreach (DetailsSchemaElement detail in other.Details)
            {
                var matches = Details.Select((a) => a.EqualTo(detail));
                if (matches == null || matches.Count() != 1)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
