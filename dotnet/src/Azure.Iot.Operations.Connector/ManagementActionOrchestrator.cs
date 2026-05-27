// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Internalizes the per-asset management-action lifecycle: discovers actions on an asset,
    /// spawns one loop per action, acquires/swaps <see cref="ManagementActionExecutor"/>s,
    /// dispatches incoming RPC requests to the user's <see cref="IManagementActionHandler"/>,
    /// reports config errors, and tears everything down when the asset goes away.
    /// </summary>
    /// <remarks>
    /// Extracted from <see cref="ConnectorWorker"/> so the base worker doesn't need to know about
    /// <see cref="ManagementActionExecutor"/>, action-loop notification semantics, or
    /// <see cref="ConfigError"/> merging. Mirrors the strategy-object pattern used by
    /// <see cref="PollingTelemetryConnectorWorker"/> + <see cref="IDatasetSampler"/>.
    /// </remarks>
    internal sealed class ManagementActionOrchestrator
    {
        private readonly IManagementActionHandlerFactory _factory;
        private readonly ILogger _logger;

        public ManagementActionOrchestrator(
            IManagementActionHandlerFactory factory,
            ILogger logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>
        /// Entry point — invoked once per <see cref="AssetAvailableEventArgs"/>. Runs every
        /// management-action loop declared on the asset until the asset goes away (the token
        /// is cancelled) or every action has been deleted.
        /// </summary>
        public async Task ServeActionsWhileAssetIsAvailableAsync(AssetAvailableEventArgs args, CancellationToken cancellationToken)
        {
            var actionTasks = new List<Task>();
            foreach (var group in args.Asset.ManagementGroups ?? Enumerable.Empty<AssetManagementGroup>())
            {
                foreach (var action in group.Actions ?? Enumerable.Empty<AssetManagementGroupAction>())
                {
                    EndpointCredentials? credentials = null;
                    if (args.Device.Endpoints?.Inbound != null
                        && args.Device.Endpoints.Inbound.TryGetValue(args.InboundEndpointName, out var inboundEndpoint))
                    {
                        credentials = args.AdrClient.GetEndpointCredentials(args.DeviceName, args.InboundEndpointName, inboundEndpoint);
                    }

                    // Connector-specific validation. Failure is reported but does NOT block handler
                    // creation — transient validation issues shouldn't permanently kill the action.
                    // The notification loop re-runs validation on every Updated* notification.
                    ConfigError? initialUserValidationError = await _factory.ValidateConfigurationAsync(
                        args.Device,
                        args.InboundEndpointName,
                        args.Asset,
                        action,
                        cancellationToken);

                    if (_factory.SupportsAction(action))
                    {
                        IManagementActionHandler handler = _factory.CreateHandler(
                            args.Device,
                            args.InboundEndpointName,
                            args.Asset,
                            action,
                            credentials);

                        var ctx = new ActionContext(
                            AssetClient: args.AssetClient,
                            Device: args.Device,
                            InboundEndpointName: args.InboundEndpointName,
                            Asset: args.Asset,
                            DeviceName: args.DeviceName,
                            AssetName: args.AssetName,
                            GroupName: group.Name,
                            Action: action,
                            Handler: handler);

                        actionTasks.Add(RunActionLoopAsync(ctx, initialUserValidationError, cancellationToken));
                    }
                    else
                    {
                        _logger.LogWarning(
                            "No handler available for action {ActionName} in group {GroupName} on asset {AssetName} (device {DeviceName}).",
                            action.Name,
                            group.Name,
                            args.AssetName,
                            args.DeviceName);
                        actionTasks.Add(RunUnsupportedActionLoopAsync(
                            args.AssetClient,
                            group.Name,
                            action.Name,
                            args.AssetName,
                            args.DeviceName,
                            cancellationToken));
                    }
                }
            }

            if (actionTasks.Count == 0)
            {
                _logger.LogInformation("No management actions declared on asset {AssetName}.", args.AssetName);
                return;
            }

            await Task.WhenAll(actionTasks);
        }

        /// <summary>
        /// Per-action loop for an action whose factory returned <c>false</c> from
        /// <see cref="IManagementActionHandlerFactory.SupportsAction"/>. Reports an
        /// <c>UnsupportedAction</c> <see cref="ConfigError"/> to ADR, then waits on
        /// notifications and re-reports on every update so the status doesn't silently
        /// lapse if ADR clears it or the action definition changes. Exits on
        /// <see cref="ManagementActionDeleted"/>.
        /// </summary>
        /// <remarks>
        /// No <see cref="ManagementActionExecutor"/> is ever subscribed by this loop, but if
        /// the SDK hands one to us via <see cref="ManagementActionUpdatedWithNewExecutor"/>
        /// we dispose it immediately so the MQTT subscription isn't leaked.
        /// </remarks>
        private async Task RunUnsupportedActionLoopAsync(
            AssetClient assetClient,
            string groupName,
            string actionName,
            string assetName,
            string deviceName,
            CancellationToken cancellationToken)
        {
            ConfigError BuildError() => new()
            {
                Code = "UnsupportedAction",
                Message = $"No handler available for action '{groupName}::{actionName}'.",
            };

            async Task ReReportAsync()
            {
                _logger.LogDebug(
                    "Unsupported action {Group}::{Action} on asset {AssetName} (device {DeviceName}) updated; re-reporting UnsupportedAction config error.",
                    groupName, actionName, assetName, deviceName);
                await ReportConfigErrorAsync(assetClient, groupName, actionName, BuildError(), cancellationToken);
                await assetClient.PauseManagementActionRuntimeHealthReportingAsync(groupName, actionName, cancellationToken);
            }

            await ReportConfigErrorAsync(assetClient, groupName, actionName, BuildError(), cancellationToken);
            await assetClient.PauseManagementActionRuntimeHealthReportingAsync(groupName, actionName, cancellationToken);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ManagementActionNotification notification =
                        await assetClient.RecvManagementActionNotificationAsync(groupName, actionName, cancellationToken);

                    switch (notification)
                    {
                        case ManagementActionUpdatedWithNewExecutor updatedWithNew:
                            // We're not going to drive this executor; drop the subscription so the
                            // broker stops delivering to a topic we'll never service.
                            if (updatedWithNew.NewExecutor is not null)
                            {
                                await updatedWithNew.NewExecutor.StopAsync(cancellationToken);
                                await updatedWithNew.NewExecutor.DisposeAsync();
                            }
                            await ReReportAsync();
                            break;

                        case ManagementActionUpdated:
                        case ManagementActionAssetUpdated:
                            await ReReportAsync();
                            break;

                        case ManagementActionDeleted:
                            _logger.LogInformation(
                                "Unsupported action {Group}::{Action} on asset {AssetName} (device {DeviceName}) deleted; ending unsupported-action loop.",
                                groupName, actionName, assetName, deviceName);
                            return;

                        default:
                            _logger.LogWarning(
                                "Unsupported action {Group}::{Action} on asset {AssetName} (device {DeviceName}) received unknown notification type {Type}",
                                groupName, actionName, assetName, deviceName, notification.GetType().Name);
                            break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // expected on shutdown / asset-unavailable
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unsupported-action loop for {Group}::{Action} on asset {AssetName} (device {DeviceName}) faulted",
                    groupName, actionName, assetName, deviceName);
                throw;
            }
        }

        /// <summary>
        /// Per-action loop: manages executor lifecycle and dispatches incoming requests to the
        /// user's <see cref="IManagementActionHandler"/>. Runs for the lifetime of the action
        /// (until deleted or the asset becomes unavailable). Translates any unhandled exception
        /// into a logged-and-rethrown event so the task doesn't fault silently inside
        /// <c>Task.WhenAll</c>.
        /// </summary>
        private async Task RunActionLoopAsync(
            ActionContext ctx,
            ConfigError? initialUserValidationError,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Starting handler for management action {Group}::{Action} on asset {AssetName} (device {DeviceName})",
                ctx.GroupName, ctx.ActionName, ctx.AssetName, ctx.DeviceName);

            ManagementActionExecutor? executor =
                await ctx.AssetClient.GetManagementActionExecutorAsync(ctx.GroupName, ctx.ActionName, cancellationToken);
            WireCallback(executor, ctx);

            // Reflect the connector-supplied validation result from startup, if any.
            if (initialUserValidationError is not null)
            {
                await ReportConfigErrorAsync(ctx.AssetClient, ctx.GroupName, ctx.ActionName, initialUserValidationError, cancellationToken);
                // Don't claim Unavailable: the device wasn't probed, the configuration is just
                // invalid. Pause runtime-health reporting so ADR sees Unknown until the next
                // notification produces a valid config.
                await ctx.AssetClient.PauseManagementActionRuntimeHealthReportingAsync(ctx.GroupName, ctx.ActionName, cancellationToken);
            }

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ManagementActionNotification notification =
                        await ctx.AssetClient.RecvManagementActionNotificationAsync(ctx.GroupName, ctx.ActionName, cancellationToken);

                    bool shouldExit = await HandleNotificationAsync(
                        ctx,
                        notification,
                        currentExecutor: executor,
                        updateExecutor: next =>
                        {
                            executor = next;
                            WireCallback(executor, ctx);
                        },
                        cancellationToken);

                    if (shouldExit) break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // expected on shutdown / asset-unavailable
            }
            catch (Exception ex)
            {
                // Without this catch the task faults silently inside Task.WhenAll(actionTasks) and is then
                // absorbed by the connector framework's user-callback wrapper.
                _logger.LogError(ex,
                    "Management action loop for {Group}::{Action} on asset {AssetName} (device {DeviceName}) faulted",
                    ctx.GroupName, ctx.ActionName, ctx.AssetName, ctx.DeviceName);
                throw;
            }
            finally
            {
                if (executor is not null)
                {
                    await executor.DisposeAsync();
                }

                await ctx.Handler.DisposeAsync();
                _logger.LogInformation(
                    "Handler for {Group}::{Action} on asset {AssetName} (device {DeviceName}) exited.",
                    ctx.GroupName, ctx.ActionName, ctx.AssetName, ctx.DeviceName);
            }
        }

        /// <summary>
        /// Wire the executor's <see cref="ManagementActionExecutor.OnRequestReceived"/>
        /// callback so incoming requests are dispatched to the handler in <paramref name="ctx"/>
        /// via <see cref="InvokeHandlerAsync"/>. No-op if <paramref name="executor"/> is null
        /// (the action's current definition is invalid; the worker is waiting for the next
        /// notification to swap it in).
        /// </summary>
        private void WireCallback(ManagementActionExecutor? executor, ActionContext ctx)
        {
            if (executor is null) return;

            executor.OnRequestReceived = (args, ct) =>
            {
                _logger.LogInformation(
                    "Received invocation for {Group}::{Action} on asset {AssetName} (device {DeviceName}) (type={ActionType}, {Bytes} bytes, content-type={ContentType})",
                    args.GroupName, args.ActionName, ctx.AssetName, ctx.DeviceName, args.ActionType, args.Payload.Length, args.ContentType);
                return InvokeHandlerAsync(ctx.Handler, args, _logger, ct);
            };
        }

        /// <summary>
        /// Invokes the handler and translates unhandled exceptions into a
        /// <see cref="ManagementActionApplicationError"/> response so the invoker sees a
        /// deterministic failure rather than a fault. <see cref="OperationCanceledException"/>
        /// is propagated unchanged so the surrounding loop can observe shutdown.
        /// Pure function over its inputs — does not touch the executor or any worker
        /// state — so it can be unit-tested without the surrounding loop.
        /// </summary>
        internal static async Task<ManagementActionResponse> InvokeHandlerAsync(
            IManagementActionHandler handler,
            ManagementActionInvokedEventArgs eventArgs,
            ILogger? logger,
            CancellationToken cancellationToken)
        {
            try
            {
                return await handler.HandleAsync(eventArgs, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogError(ex, "Handler threw for {Group}::{Action}", eventArgs.GroupName, eventArgs.ActionName);
                return new ManagementActionResponse
                {
                    Payload = ReadOnlySequence<byte>.Empty,
                    ContentType = "application/json",
                    ApplicationError = new ManagementActionApplicationError
                    {
                        ErrorCode = "InternalError",
                        ErrorPayload = $"Handler failed: {ex.Message}",
                    },
                };
            }
        }

        /// <summary>
        /// Handle a lifecycle notification. Returns <c>true</c> if the per-action loop should exit.
        /// </summary>
        private async Task<bool> HandleNotificationAsync(
            ActionContext ctx,
            ManagementActionNotification notification,
            ManagementActionExecutor? currentExecutor,
            Action<ManagementActionExecutor?> updateExecutor,
            CancellationToken cancellationToken)
        {
            switch (notification)
            {
                case ManagementActionUpdated updated:
                    _logger.LogInformation(
                        "{Group}::{Action} on asset {AssetName} (device {DeviceName}) definition updated (same topic). sdkError={Error}",
                        ctx.GroupName, ctx.ActionName, ctx.AssetName, ctx.DeviceName, updated.Error);
                    await ctx.AssetClient.PauseManagementActionRuntimeHealthReportingAsync(ctx.GroupName, ctx.ActionName, cancellationToken);
                    await RevalidateAndReportAsync(ctx, updated.Error, cancellationToken);
                    return false;

                case ManagementActionUpdatedWithNewExecutor updatedWithNew:
                    _logger.LogInformation(
                        "{Group}::{Action} on asset {AssetName} (device {DeviceName}) definition updated — swapping to new executor. sdkError={Error}",
                        ctx.GroupName, ctx.ActionName, ctx.AssetName, ctx.DeviceName, updatedWithNew.Error);
                    await ctx.AssetClient.PauseManagementActionRuntimeHealthReportingAsync(ctx.GroupName, ctx.ActionName, cancellationToken);

                    if (currentExecutor is not null)
                    {
                        // Unsubscribe so the broker stops delivering new requests, then wait for
                        // in-flight invocations to wind down. In-flight callbacks complete naturally
                        // or are bounded by the underlying CommandExecutor's ExecutionTimeout.
                        await currentExecutor.StopAsync(cancellationToken);
                        await currentExecutor.DisposeAsync();
                    }

                    updateExecutor(updatedWithNew.NewExecutor);
                    await RevalidateAndReportAsync(ctx, updatedWithNew.Error, cancellationToken);
                    return false;

                case ManagementActionAssetUpdated assetUpdated:
                    _logger.LogInformation(
                        "{Group}::{Action} on asset {AssetName} (device {DeviceName}): parent asset updated. error={Error}",
                        ctx.GroupName, ctx.ActionName, ctx.AssetName, ctx.DeviceName, assetUpdated.Error);
                    await ctx.AssetClient.PauseManagementActionRuntimeHealthReportingAsync(ctx.GroupName, ctx.ActionName, cancellationToken);
                    await RevalidateAndReportAsync(ctx, assetUpdated.Error, cancellationToken);
                    return false;

                case ManagementActionDeleted:
                    _logger.LogInformation(
                        "{Group}::{Action} on asset {AssetName} (device {DeviceName}) deleted.",
                        ctx.GroupName, ctx.ActionName, ctx.AssetName, ctx.DeviceName);
                    if (currentExecutor is not null)
                    {
                        await currentExecutor.StopAsync(cancellationToken);
                        await currentExecutor.DisposeAsync();
                    }
                    return true;

                default:
                    _logger.LogWarning(
                        "{Group}::{Action} on asset {AssetName} (device {DeviceName}) received unknown notification type {Type}",
                        ctx.GroupName, ctx.ActionName, ctx.AssetName, ctx.DeviceName, notification.GetType().Name);
                    return false;
            }
        }

        /// <summary>
        /// Re-runs connector-supplied <see cref="IManagementActionHandlerFactory.ValidateConfigurationAsync"/>,
        /// merges its result with <paramref name="sdkError"/>, and reports the resulting config
        /// state to ADR. On success the action is reported <c>Available</c>; on a config error the
        /// action's config error is reported and runtime-health reporting is paused so the
        /// runtime-health status lapses to <c>Unknown</c> (we cannot probe the device, so we make
        /// no claim about it — only the config error itself is surfaced).
        /// </summary>
        private async Task RevalidateAndReportAsync(ActionContext ctx, ConfigError? sdkError, CancellationToken cancellationToken)
        {
            ConfigError? userError = await _factory.ValidateConfigurationAsync(
                ctx.Device, ctx.InboundEndpointName, ctx.Asset, ctx.Action, cancellationToken);
            ConfigError? combined = MergeConfigErrors(sdkError, userError);

            await ReportConfigErrorAsync(ctx.AssetClient, ctx.GroupName, ctx.ActionName, combined, cancellationToken);
            if (combined is null)
            {
                await ctx.AssetClient.ReportManagementActionRuntimeHealthAsync(
                    ctx.GroupName, ctx.ActionName,
                    new ConnectorRuntimeHealth { Status = HealthStatus.Available },
                    cancellationToken: cancellationToken);
            }
            else
            {
                // Config is invalid → we cannot determine runtime health, so stay silent.
                // ADR will let the runtime-health status lapse to Unknown rather than us
                // falsely asserting Unavailable for a device we never probed.
                await ctx.AssetClient.PauseManagementActionRuntimeHealthReportingAsync(ctx.GroupName, ctx.ActionName, cancellationToken);
            }
        }

        /// <summary>
        /// Updates the action's <c>Config</c> status via <see cref="AssetClient"/>. Pass <c>null</c>
        /// for <paramref name="validationError"/> to clear an existing configuration error.
        /// </summary>
        private static Task ReportConfigErrorAsync(
            AssetClient assetClient,
            string groupName,
            string actionName,
            ConfigError? validationError,
            CancellationToken cancellationToken)
            => assetClient.GetAndUpdateAssetStatusAsync(
                current =>
                {
                    current.Config ??= new ConfigStatus();
                    current.Config.LastTransitionTime = DateTime.UtcNow;
                    current.UpdateManagementGroupStatus(
                        groupName,
                        new AssetManagementGroupActionStatus
                        {
                            Name = actionName,
                            Error = validationError,
                        });
                    return current;
                },
                onlyIfChanged: true,
                commandTimeout: null,
                cancellationToken);

        /// <summary>
        /// Combines an SDK-supplied <see cref="ConfigError"/> with a connector-supplied one. Returns
        /// <c>null</c> if both are null; one of them if the other is null; otherwise a new
        /// <see cref="ConfigError"/> that concatenates messages and unions <see cref="ConfigError.Details"/>,
        /// preferring the SDK's <see cref="ConfigError.Code"/> if present.
        /// </summary>
        internal static ConfigError? MergeConfigErrors(ConfigError? sdkError, ConfigError? userError)
        {
            if (sdkError is null) return userError;
            if (userError is null) return sdkError;

            var details = new List<ConfigErrorDetails>();
            if (sdkError.Details is not null) details.AddRange(sdkError.Details);
            if (userError.Details is not null) details.AddRange(userError.Details);

            return new ConfigError
            {
                Code = sdkError.Code ?? userError.Code,
                Message = $"[sdk] {sdkError.Message}\n[connector] {userError.Message}",
                Details = details.Count > 0 ? details : null,
            };
        }

        /// <summary>
        /// Internal per-action bundle. Captures everything that's fixed for the lifetime of one
        /// management action's loop so we don't have to thread 8+ parameters through every
        /// internal helper. Built once in <see cref="ManagementActionOrchestrator.ServeActionsWhileAssetIsAvailableAsync"/>.
        /// </summary>
        private sealed record ActionContext(
            AssetClient AssetClient,
            Device Device,
            string InboundEndpointName,
            Asset Asset,
            string DeviceName,
            string AssetName,
            string GroupName,
            AssetManagementGroupAction Action,
            IManagementActionHandler Handler)
        {
            /// <summary>Shorthand for <c>Action.Name</c>; the action's own name is the action name.</summary>
            public string ActionName => Action.Name;
        }
    }
}
