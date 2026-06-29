// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Options that control the behavior of a delete operation.
/// </summary>
public class DeleteOptions
{
    /// <summary>
    /// If specified, the request fails when the current epoch doesn't match.
    /// </summary>
    public ulong? ExpectedEpoch { get; set; }
}
