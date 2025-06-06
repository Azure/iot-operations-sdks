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
}
