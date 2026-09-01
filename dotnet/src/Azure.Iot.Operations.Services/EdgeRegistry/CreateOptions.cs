// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Options that control the behavior of a create operation.
/// </summary>
public class CreateOptions
{
    /// <summary>
    /// Whether the Edge Registry should persist the created entity to disk. Defaults to <c>true</c>.
    /// </summary>
    /// <remarks>
    /// Persistence requires a broker deployed with persistence enabled; it is otherwise ignored. An
    /// entity that has been persisted can't later be made non-persistent.
    /// </remarks>
    public bool Persist { get; set; } = true;
}
