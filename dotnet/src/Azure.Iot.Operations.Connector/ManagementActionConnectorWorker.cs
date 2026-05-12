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

                    IManagementActionHandler handler = _handlerFactory.CreateHandler(
                        args.Device,
                        args.InboundEndpointName,
                        args.Asset,
                        action,
                        credentials);

                    actionTasks.Add(RunActionLoopAsync(
                        args.AssetClient,
                        args.DeviceName,
                        args.AssetName,
                        group.Name,
                        action,
                        handler,
                        cancellationToken));
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
        /// (until deleted or the asset becomes unavailable).
        /// </summary>
        private async Task RunActionLoopAsync(
            AssetClient assetClient,
            string deviceName,
            string assetName,
            string groupName,
            AssetManagementGroupAction action,
            IManagementActionHandler handler,
            CancellationToken cancellationToken)
        {
            try
            {
                await RunActionLoopCoreAsync(
                    assetClient, groupName, action, handler, cancellationToken);
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
                    groupName, action.Name, assetName, deviceName);
                throw;
            }
        }

        private async Task RunActionLoopCoreAsync(
            AssetClient assetClient,
            string groupName,
            AssetManagementGroupAction action,
            IManagementActionHandler handler,
            CancellationToken cancellationToken)
        {
            string actionName = action.Name;
            _logger.LogInformation("Starting handler for management action {Group}::{Action}", groupName, actionName);

            ManagementActionExecutor? executor =
                await assetClient.GetManagementActionExecutorAsync(groupName, actionName, cancellationToken);
            WireCallback(executor, handler, action);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ManagementActionNotification notification =
                        await assetClient.RecvManagementActionNotificationAsync(groupName, actionName, cancellationToken);

                    bool shouldExit = await HandleNotificationAsync(
                        assetClient, groupName, actionName, notification,
                        currentExecutor: executor,
                        updateExecutor: next =>
                        {
                            executor = next;
                            WireCallback(executor, handler, action);
                        },
                        cancellationToken);

                    if (shouldExit) break;
                }
            }
            finally
            {
                if (executor is not null)
                {
                    await executor.DisposeAsync();
                }

                await handler.DisposeAsync();
                _logger.LogInformation("Handler for {Group}::{Action} exited.", groupName, actionName);
            }
        }

        /// <summary>
        /// Wire the executor's <see cref="ManagementActionExecutor.OnRequestReceived"/>
        /// callback so incoming requests are dispatched to <paramref name="handler"/> via
        /// <see cref="InvokeHandlerAsync"/>. No-op if <paramref name="executor"/> is null
        /// (the action's current definition is invalid; the worker is waiting for the next
        /// notification to swap it in).
        /// </summary>
        private void WireCallback(
            ManagementActionExecutor? executor,
            IManagementActionHandler handler,
            AssetManagementGroupAction action)
        {
            if (executor is null) return;

            executor.OnRequestReceived = (args, ct) =>
            {
                _logger.LogInformation(
                    "Received invocation for {Group}::{Action} (type={ActionType}, {Bytes} bytes, content-type={ContentType})",
                    args.GroupName, args.ActionName, action.ActionType, args.Payload.Length, args.ContentType);
                return InvokeHandlerAsync(handler, action.ActionType, args, _logger, ct);
            };
        }

        /// <summary>
        /// Routes <paramref name="eventArgs"/> to the appropriate <see cref="IManagementActionHandler"/>
        /// method based on <paramref name="actionType"/> and translates unsupported types and
        /// unhandled exceptions into <see cref="ManagementActionApplicationError"/> responses.
        /// A <see cref="ManagementActionNotSupportedException"/> thrown by the handler is
        /// translated into an <c>UnsupportedActionType</c> error so handlers that only implement
        /// a subset of <see cref="IManagementActionHandler"/>'s methods can decline cleanly.
        /// Pure function over its inputs &mdash; does not touch the executor or any worker
        /// state &mdash; so it can be unit-tested without the surrounding loop.
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
            AssetClient assetClient,
            string groupName,
            string actionName,
            ManagementActionNotification notification,
            ManagementActionExecutor? currentExecutor,
            Action<ManagementActionExecutor?> updateExecutor,
            CancellationToken cancellationToken)
        {
            switch (notification)
            {
                case ManagementActionUpdated updated:
                    _logger.LogInformation(
                        "{Group}::{Action} definition updated (same topic). error={Error}",
                        groupName, actionName, updated.Error);
                    await assetClient.PauseManagementActionRuntimeHealthReportingAsync(groupName, actionName, cancellationToken);
                    await ReportActionConfigStatusAsync(assetClient, groupName, actionName, validationError: updated.Error, cancellationToken);
                    if (updated.Error is null)
                    {
                        await ReportActionAvailableAsync(assetClient, groupName, actionName, cancellationToken);
                    }
                    else
                    {
                        await ReportActionUnavailableAsync(assetClient, groupName, actionName, updated.Error.Message, cancellationToken);
                    }
                    return false;

                case ManagementActionUpdatedWithNewExecutor updatedWithNew:
                    _logger.LogInformation(
                        "{Group}::{Action} definition updated — swapping to new executor. error={Error}",
                        groupName, actionName, updatedWithNew.Error);
                    await assetClient.PauseManagementActionRuntimeHealthReportingAsync(groupName, actionName, cancellationToken);

                    if (currentExecutor is not null)
                    {
                        // Unsubscribe so the broker stops delivering new requests, then wait for
                        // in-flight invocations to wind down. In-flight callbacks complete naturally
                        // or are bounded by the underlying CommandExecutor's ExecutionTimeout.
                        await currentExecutor.StopAsync(cancellationToken);
                        await currentExecutor.DisposeAsync();
                    }

                    updateExecutor(updatedWithNew.NewExecutor);
                    await ReportActionConfigStatusAsync(assetClient, groupName, actionName, validationError: updatedWithNew.Error, cancellationToken);
                    if (updatedWithNew.Error is null)
                    {
                        await ReportActionAvailableAsync(assetClient, groupName, actionName, cancellationToken);
                    }
                    else
                    {
                        await ReportActionUnavailableAsync(assetClient, groupName, actionName, updatedWithNew.Error.Message, cancellationToken);
                    }
                    return false;

                case ManagementActionAssetUpdated assetUpdated:
                    _logger.LogInformation(
                        "{Group}::{Action}: parent asset updated. error={Error}",
                        groupName, actionName, assetUpdated.Error);
                    return false;

                case ManagementActionDeleted:
                    _logger.LogInformation("{Group}::{Action} deleted.", groupName, actionName);
                    if (currentExecutor is not null)
                    {
                        await currentExecutor.StopAsync(cancellationToken);
                        await currentExecutor.DisposeAsync();
                    }
                    return true;

                default:
                    _logger.LogWarning(
                        "{Group}::{Action} received unknown notification type {Type}",
                        groupName, actionName, notification.GetType().Name);
                    return false;
            }
        }

        private static Task ReportActionAvailableAsync(
            AssetClient assetClient, string groupName, string actionName, CancellationToken cancellationToken)
            => assetClient.ReportManagementActionRuntimeHealthAsync(
                groupName, actionName,
                new ConnectorRuntimeHealth { Status = HealthStatus.Available },
                cancellationToken: cancellationToken);

        private static Task ReportActionUnavailableAsync(
            AssetClient assetClient, string groupName, string actionName, string? message, CancellationToken cancellationToken)
            => assetClient.ReportManagementActionRuntimeHealthAsync(
                groupName, actionName,
                new ConnectorRuntimeHealth
                {
                    Status = HealthStatus.Unavailable,
                    ReasonCode = "ConfigError",
                    Message = message,
                },
                cancellationToken: cancellationToken);

        private static Task ReportActionConfigStatusAsync(
            AssetClient assetClient, string groupName, string actionName,
            ConfigError? validationError, CancellationToken cancellationToken)
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
    }
}

