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
    /// <remarks>
    /// The three common variants (<see cref="ManagementActionUpdated"/>,
    /// <see cref="ManagementActionAssetUpdated"/>, <see cref="ManagementActionDeleted"/>)
    /// mirror the Rust SDK's <c>DataOperationNotification</c> (Updated / AssetUpdated / Deleted).
    /// <see cref="ManagementActionUpdatedWithNewExecutor"/> is management-action-specific
    /// (datasets/events/streams don't have executors).
    /// <para/>
    /// TODO: When per-component notifications are added for datasets, events, and streams,
    /// consider extracting the shared variants into a common base or generic type (e.g.
    /// <c>ComponentNotification&lt;T&gt;</c>) so all component types share a consistent shape.
    /// </remarks>
    public abstract record ManagementActionNotification;

    /// <summary>
    /// The management action definition was updated in place — same request topic,
    /// same executor remains valid. The connector should re-validate the definition,
    /// re-report schemas, and pause + resume health reporting.
    /// </summary>
    /// <param name="Error">
    /// Non-null if the updated definition failed validation. The executor still
    /// runs, but the connector should report the config error back via asset status.
    /// </param>
    public sealed record ManagementActionUpdated(ConfigError? Error) : ManagementActionNotification;

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
    public sealed record ManagementActionUpdatedWithNewExecutor(
        ManagementActionExecutor? NewExecutor,
        ConfigError? Error) : ManagementActionNotification;

    /// <summary>
    /// The parent asset was updated but this specific management action's definition
    /// is unchanged. The connector may need to re-evaluate surrounding state
    /// (device context, other actions on the asset).
    /// </summary>
    public sealed record ManagementActionAssetUpdated(ConfigError? Error) : ManagementActionNotification;

    /// <summary>
    /// The management action was removed from the asset definition, or the asset
    /// itself was deleted. No further requests will be delivered; the connector
    /// should stop and dispose the executor and exit its per-action loop.
    /// </summary>
    public sealed record ManagementActionDeleted : ManagementActionNotification;
}

