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

        /// <summary>Budget for draining an outdated executor's backlog before forcing dispose.</summary>
        private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

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
            base.WhileDeviceIsAvailable = WhileDeviceAvailableAsync;
            base.WhileAssetIsAvailable = WhileAssetAvailableAsync;
        }

        private Task WhileDeviceAvailableAsync(DeviceAvailableEventArgs args, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Device {DeviceName} (endpoint {EndpointName}) is available.",
                args.DeviceName,
                args.InboundEndpointName);
            return Task.CompletedTask;
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
                        args.DeviceName,
                        args.Device,
                        args.InboundEndpointName,
                        args.AssetName,
                        args.Asset,
                        group.Name,
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
            string actionName = action.Name;
            _logger.LogInformation("Starting handler for management action {Group}::{Action}", groupName, actionName);

            ManagementActionExecutor? executor =
                await assetClient.GetManagementActionExecutorAsync(groupName, actionName, cancellationToken);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Task<ManagementActionNotification> recvNotificationTask =
                        assetClient.RecvManagementActionNotificationAsync(groupName, actionName, cancellationToken);

                    if (executor is null)
                    {
                        ManagementActionNotification notification = await recvNotificationTask;
                        bool shouldExit = await HandleNotificationAsync(
                            assetClient, groupName, actionName, notification,
                            currentExecutor: null,
                            updateExecutor: next => executor = next,
                            cancellationToken);
                        if (shouldExit) break;
                        continue;
                    }

                    Task<ManagementActionRequest?> recvRequestTask =
                        executor.RecvRequestAsync(cancellationToken);

                    Task completed = await Task.WhenAny(recvRequestTask, recvNotificationTask);

                    if (completed == recvRequestTask)
                    {
                        ManagementActionRequest? request = await recvRequestTask;
                        if (request is null)
                        {
                            _logger.LogInformation("Executor for {Group}::{Action} shut down.", groupName, actionName);
                            break;
                        }

                        await DispatchRequestAsync(
                            handler, request, deviceName, assetName, groupName, action, cancellationToken);
                    }
                    else
                    {
                        ManagementActionNotification notification = await recvNotificationTask;
                        bool shouldExit = await HandleNotificationAsync(
                            assetClient, groupName, actionName, notification,
                            currentExecutor: executor,
                            updateExecutor: next => executor = next,
                            cancellationToken);
                        if (shouldExit) break;
                    }
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
        /// Dispatches a request to the appropriate handler method based on the action type,
        /// and sends the response (or error) back to the invoker.
        /// </summary>
        private async Task DispatchRequestAsync(
            IManagementActionHandler handler,
            ManagementActionRequest request,
            string deviceName,
            string assetName,
            string groupName,
            AssetManagementGroupAction action,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Received invocation for {Group}::{Action} (type={ActionType}, {Bytes} bytes, content-type={ContentType})",
                groupName, action.Name, action.ActionType, request.Payload.Length, request.ContentType);

            var eventArgs = new ManagementActionInvokedEventArgs
            {
                GroupName = groupName,
                ActionName = action.Name,
                ActionType = action.ActionType,
                Payload = request.Payload,
                ContentType = request.ContentType,
                FormatIndicator = request.FormatIndicator,
                CustomUserData = request.CustomUserData,
                Timestamp = request.Timestamp,
                InvokerId = request.InvokerId,
                TopicTokens = request.TopicTokens,
                AssetName = assetName,
                DeviceName = deviceName,
            };

            ManagementActionResponse response;
            try
            {
                response = action.ActionType switch
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
                            ErrorPayload = $"Action type '{action.ActionType}' is not supported.",
                        },
                    },
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Handler threw for {Group}::{Action}", groupName, action.Name);
                response = new ManagementActionResponse
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

            try
            {
                await request.CompleteAsync(response, cancellationToken);
            }
            finally
            {
                await request.DisposeAsync();
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
                        await DrainAndDisposeExecutorAsync(
                            currentExecutor, groupName, actionName,
                            errorCode: "ManagementActionDefinitionOutdated",
                            errorMessage: "Management action definition changed; this request was received on the previous topic.",
                            cancellationToken);
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
                        await DrainAndDisposeExecutorAsync(
                            currentExecutor, groupName, actionName,
                            errorCode: "ManagementActionDeleted",
                            errorMessage: "Management action definition deleted.",
                            cancellationToken);
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

        private async Task DrainAndDisposeExecutorAsync(
            ManagementActionExecutor executor,
            string groupName,
            string actionName,
            string errorCode,
            string errorMessage,
            CancellationToken cancellationToken)
        {
            using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            drainCts.CancelAfter(DrainTimeout);

            int drained = 0;
            try
            {
                while (true)
                {
                    ManagementActionRequest? stale;
                    try
                    {
                        stale = await executor.RecvRequestAsync(drainCts.Token);
                    }
                    catch (OperationCanceledException) when (drainCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning(
                            "{Group}::{Action}: drain timed out after {Timeout} ({Drained} drained); forcing dispose.",
                            groupName, actionName, DrainTimeout, drained);
                        break;
                    }

                    if (stale is null) break;

                    var errorResponse = new ManagementActionResponse
                    {
                        Payload = ReadOnlySequence<byte>.Empty,
                        ContentType = "application/json",
                        CloudEvent = null,
                        ApplicationError = new ManagementActionApplicationError
                        {
                            ErrorCode = errorCode,
                            ErrorPayload = errorMessage,
                        },
                    };

                    try
                    {
                        await stale.CompleteAsync(errorResponse, drainCts.Token);
                        drained++;
                    }
                    finally
                    {
                        await stale.DisposeAsync();
                    }
                }
            }
            finally
            {
                await executor.DisposeAsync();
            }

            _logger.LogInformation(
                "{Group}::{Action}: drained {Drained} stale request(s) from old executor.",
                groupName, actionName, drained);
        }
    }
}

