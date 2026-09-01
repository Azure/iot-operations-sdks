// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Lifecycle notification for a single management action. Obtain via
    /// <see cref="AssetClient.RecvManagementActionNotificationAsync(string, string, System.Threading.CancellationToken)"/>.
    /// Discriminated union — use pattern matching (<c>switch</c>) to dispatch.
    /// </summary>
    internal abstract record ManagementActionNotification;

    /// <summary>
    /// The management action definition was updated in place — same request topic,
    /// same executor remains valid. The connector should re-validate the definition,
    /// re-report schemas, and pause + resume health reporting.
    /// </summary>
    /// <param name="Error">
    /// Non-null if the updated definition failed validation. The executor still
    /// runs, but the connector should report the config error back via asset status.
    /// </param>
    internal sealed record ManagementActionUpdated(ConfigError? Error) : ManagementActionNotification;

    /// <summary>
    /// The management action definition was updated in a way that requires a new
    /// executor (e.g. the request topic changed). The old executor must be stopped
    /// and disposed; <see cref="ManagementActionExecutor.OnRequestReceived"/> must
    /// be wired on <see cref="NewExecutor"/> before requests can resume.
    /// </summary>
    /// <param name="NewExecutor">
    /// Replacement executor. May be <c>null</c> if <paramref name="Error"/> indicates
    /// the new definition is invalid and no executor could be built.
    /// </param>
    /// <param name="Error">
    /// Non-null if the updated definition failed validation.
    /// </param>
    internal sealed record ManagementActionUpdatedWithNewExecutor(
        ManagementActionExecutor? NewExecutor,
        ConfigError? Error) : ManagementActionNotification;

    /// <summary>
    /// The parent asset was updated but this specific management action's definition
    /// is unchanged. The connector may need to re-evaluate surrounding state
    /// (asset context, asset defaults).
    /// </summary>
    internal sealed record ManagementActionAssetUpdated(ConfigError? Error) : ManagementActionNotification;

    /// <summary>
    /// The management action was removed from the asset definition, or the asset
    /// itself was deleted. No further requests will be delivered; the connector
    /// should stop and dispose the executor and exit its per-action loop.
    /// </summary>
    internal sealed record ManagementActionDeleted : ManagementActionNotification;
}

