// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Indicates whether validation was performed, and if not, the reason why not (e.g.,
/// "unsupported format", "validation disabled").
/// </summary>
public class ValidationStatus
{
    /// <summary>
    /// True if validation was performed and the entity adheres to the rules; false if validation was not performed.
    /// </summary>
    public required bool Validated { get; set; }

    /// <summary>
    /// If validation was not performed, the reason why not. MUST be present if validated is false. MUST NOT be present if validated is true.
    /// </summary>
    public string? Reason { get; set; }
}
