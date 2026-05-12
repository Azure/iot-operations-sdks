// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Unified error object for Edge Registry operations.
/// Normalizes EdgeRegistryError, SchemaExtensionError, and ThingDescriptionExtensionError.
/// </summary>
public class ServiceError
{
    /// <summary>
    /// HTTP-style response code.
    /// </summary>
    public ulong Code { get; set; }

    /// <summary>
    /// Detailed error description.
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    /// The XID of the entity that caused the error.
    /// </summary>
    public string Instance { get; set; } = default!;

    /// <summary>
    /// HTTP-style response status text.
    /// </summary>
    public string Status { get; set; } = default!;

    /// <summary>
    /// Short human-readable error title.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// URI identifying the error type.
    /// </summary>
    public string TypeUri { get; set; } = default!;
}
