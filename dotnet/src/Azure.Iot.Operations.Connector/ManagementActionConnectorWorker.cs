// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Base connector worker for management actions. Extends <see cref="ConnectorWorker"/> to
    /// internalize all per-action lifecycle management (executor acquisition, notification
    /// handling, drain-and-dispose, health/config reporting). User code only needs to provide
    /// an <see cref="IManagementActionHandlerFactory"/> whose handlers implement the actual
    /// device communication.
    /// </summary>
    /// <remarks>
    /// Mirrors the pattern of <see cref="PollingTelemetryConnectorWorker"/> which internalizes
    /// polling logic and delegates sampling to <see cref="IDatasetSampler"/>.
    /// </remarks>
    public class ManagementActionConnectorWorker : ConnectorWorker
    {
        private readonly IManagementActionHandlerFactory _handlerFactory;


        public ManagementActionConnectorWorker(
            ApplicationContext applicationContext,
            ILogger<ConnectorWorker> logger,
            IMqttClient mqttClient,
            IManagementActionHandlerFactory handlerFactory,
            IMessageSchemaProvider messageSchemaProvider,
            IAzureDeviceRegistryClientWrapperProvider adrClientFactory,
            IConnectorLeaderElectionConfigurationProvider? leaderElectionConfigurationProvider = null)
            : base(applicationContext, logger, mqttClient, messageSchemaProvider, adrClientFactory, leaderElectionConfigurationProvider)
        {
            _handlerFactory = handlerFactory;
            base.WhileAssetIsAvailable = WhileAssetAvailableAsync;
        }

        private async Task WhileAssetAvailableAsync(AssetAvailableEventArgs args, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Asset {AssetName} on device {DeviceName} is available.",
                args.AssetName,
                args.DeviceName);

            var adrClient = _adrClient ?? throw new InvalidOperationException("ADR client not initialized.");

            var actionTasks = new List<Task>();
            foreach (var group in args.Asset.ManagementGroups ?? Enumerable.Empty<AssetManagementGroup>())
            {
                foreach (var action in group.Actions ?? Enumerable.Empty<AssetManagementGroupAction>())
                {
                    EndpointCredentials? credentials = null;
                    if (args.Device.Endpoints?.Inbound != null
                        && args.Device.Endpoints.Inbound.TryGetValue(args.InboundEndpointName, out var inboundEndpoint))
                    {
                        credentials = adrClient.GetEndpointCredentials(args.DeviceName, args.InboundEndpointName, inboundEndpoint);
                    }

                    var statusReporter = new ManagementActionStatusReporter(args.AssetClient, group.Name, action.Name);

                    // Connector-specific validation. Failure is reported but does NOT block handler
                    // creation — transient validation issues shouldn't permanently kill the action.
                    // The notification loop re-runs validation on every Updated* notification.
                    ConfigError? initialUserValidationError = await _handlerFactory.ValidateConfigurationAsync(
                        args.Device,
                        args.InboundEndpointName,
                        args.Asset,
                        action,
                        cancellationToken);

                    IManagementActionHandler handler = _handlerFactory.CreateHandler(
                        args.Device,
                        args.InboundEndpointName,
                        args.Asset,
                        action,
                        credentials,
                        statusReporter);

                    var ctx = new ActionContext(
                        AssetClient: args.AssetClient,
                        Device: args.Device,
                        InboundEndpointName: args.InboundEndpointName,
                        Asset: args.Asset,
                        DeviceName: args.DeviceName,
                        AssetName: args.AssetName,
                        GroupName: group.Name,
                        Action: action,
                        Handler: handler,
                        StatusReporter: statusReporter);

                    actionTasks.Add(RunActionLoopAsync(ctx, initialUserValidationError, cancellationToken));
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
            _logger.LogInformation("Starting handler for management action {Group}::{Action}", ctx.GroupName, ctx.ActionName);

            ManagementActionExecutor? executor =
                await ctx.AssetClient.GetManagementActionExecutorAsync(ctx.GroupName, ctx.ActionName, cancellationToken);
            WireCallback(executor, ctx);

            // Reflect the connector-supplied validation result from startup, if any.
            if (initialUserValidationError is not null)
            {
                await ctx.StatusReporter.ReportConfigErrorAsync(initialUserValidationError, cancellationToken);
                await ctx.StatusReporter.ReportUnavailableAsync(initialUserValidationError.Message, cancellationToken);
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
                _logger.LogInformation("Handler for {Group}::{Action} exited.", ctx.GroupName, ctx.ActionName);
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
                    "Received invocation for {Group}::{Action} (type={ActionType}, {Bytes} bytes, content-type={ContentType})",
                    args.GroupName, args.ActionName, ctx.Action.ActionType, args.Payload.Length, args.ContentType);
                return InvokeHandlerAsync(ctx.Handler, ctx.Action.ActionType, args, _logger, ct);
            };
        }

        /// <summary>
        /// Routes <paramref name="eventArgs"/> to the appropriate <see cref="IManagementActionHandler"/>
        /// method based on <paramref name="actionType"/> and translates unsupported types and
        /// unhandled exceptions into <see cref="ManagementActionApplicationError"/> responses.
        /// A <see cref="ManagementActionNotSupportedException"/> thrown by the handler is
        /// translated into an <c>UnsupportedActionType</c> error so handlers that only implement
        /// a subset of <see cref="IManagementActionHandler"/>'s methods can decline cleanly.
        /// Pure function over its inputs — does not touch the executor or any worker
        /// state — so it can be unit-tested without the surrounding loop.
        /// </summary>
        internal static async Task<ManagementActionResponse> InvokeHandlerAsync(
            IManagementActionHandler handler,
            AssetManagementGroupActionType actionType,
            ManagementActionInvokedEventArgs eventArgs,
            ILogger? logger,
            CancellationToken cancellationToken)
        {
            try
            {
                return actionType switch
                {
                    AssetManagementGroupActionType.Call => await handler.HandleCallAsync(eventArgs, cancellationToken),
                    AssetManagementGroupActionType.Read => await handler.HandleReadAsync(eventArgs, cancellationToken),
                    AssetManagementGroupActionType.Write => await handler.HandleWriteAsync(eventArgs, cancellationToken),
                    _ => new ManagementActionResponse
                    {
                        Payload = ReadOnlySequence<byte>.Empty,
                        ContentType = "application/json",
                        CloudEvent = null,
                        ApplicationError = new ManagementActionApplicationError
                        {
                            ErrorCode = "UnsupportedActionType",
                            ErrorPayload = $"Action type '{actionType}' is not supported.",
                        },
                    },
                };
            }
            catch (ManagementActionNotSupportedException ex)
            {
                logger?.LogWarning(ex,
                    "Handler reported unsupported action type for {Group}::{Action} (type={ActionType})",
                    ex.GroupName, ex.ActionName, actionType);
                return new ManagementActionResponse
                {
                    Payload = ReadOnlySequence<byte>.Empty,
                    ContentType = "application/json",
                    CloudEvent = null,
                    ApplicationError = new ManagementActionApplicationError
                    {
                        ErrorCode = "UnsupportedActionType",
                        ErrorPayload = ex.Message,
                    },
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogError(ex, "Handler threw for {Group}::{Action}", eventArgs.GroupName, eventArgs.ActionName);
                return new ManagementActionResponse
                {
                    Payload = ReadOnlySequence<byte>.Empty,
                    ContentType = "application/json",
                    CloudEvent = null,
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
                        "{Group}::{Action} definition updated (same topic). sdkError={Error}",
                        ctx.GroupName, ctx.ActionName, updated.Error);
                    await ctx.AssetClient.PauseManagementActionRuntimeHealthReportingAsync(ctx.GroupName, ctx.ActionName, cancellationToken);
                    await RevalidateAndReportAsync(ctx, updated.Error, cancellationToken);
                    return false;

                case ManagementActionUpdatedWithNewExecutor updatedWithNew:
                    _logger.LogInformation(
                        "{Group}::{Action} definition updated — swapping to new executor. sdkError={Error}",
                        ctx.GroupName, ctx.ActionName, updatedWithNew.Error);
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
                        "{Group}::{Action}: parent asset updated. error={Error}",
                        ctx.GroupName, ctx.ActionName, assetUpdated.Error);
                    return false;

                case ManagementActionDeleted:
                    _logger.LogInformation("{Group}::{Action} deleted.", ctx.GroupName, ctx.ActionName);
                    if (currentExecutor is not null)
                    {
                        await currentExecutor.StopAsync(cancellationToken);
                        await currentExecutor.DisposeAsync();
                    }
                    return true;

                default:
                    _logger.LogWarning(
                        "{Group}::{Action} received unknown notification type {Type}",
                        ctx.GroupName, ctx.ActionName, notification.GetType().Name);
                    return false;
            }
        }

        /// <summary>
        /// Re-runs connector-supplied <see cref="IManagementActionHandlerFactory.ValidateConfigurationAsync"/>,
        /// merges its result with <paramref name="sdkError"/>, and reports both the config error
        /// and the resulting Available / Unavailable health to ADR.
        /// </summary>
        private async Task RevalidateAndReportAsync(ActionContext ctx, ConfigError? sdkError, CancellationToken cancellationToken)
        {
            ConfigError? userError = await _handlerFactory.ValidateConfigurationAsync(
                ctx.Device, ctx.InboundEndpointName, ctx.Asset, ctx.Action, cancellationToken);
            ConfigError? combined = MergeConfigErrors(sdkError, userError);

            await ctx.StatusReporter.ReportConfigErrorAsync(combined, cancellationToken);
            if (combined is null)
            {
                await ctx.StatusReporter.ReportAvailableAsync(cancellationToken);
            }
            else
            {
                await ctx.StatusReporter.ReportUnavailableAsync(combined.Message, cancellationToken);
            }
        }

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
        /// internal helper. Built once in <see cref="WhileAssetAvailableAsync"/>.
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
            IManagementActionHandler Handler,
            IManagementActionStatusReporter StatusReporter)
        {
            /// <summary>Shorthand for <c>Action.Name</c>; the action's own name is the action name.</summary>
            public string ActionName => Action.Name;
        }
    }
}

