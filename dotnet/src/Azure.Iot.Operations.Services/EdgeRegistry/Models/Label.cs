// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// A key-value pair used for labeling.
/// </summary>
public class Label
{
    /// <summary>
    /// The label key.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// The label value.
    /// </summary>
    public required string Value { get; set; }
}
