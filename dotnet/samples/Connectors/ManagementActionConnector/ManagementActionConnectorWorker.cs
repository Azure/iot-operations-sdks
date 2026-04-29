// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace ManagementActionConnector
{
    /// <summary>
    /// Minimal connector worker exercising the draft Management Action API defined in
    /// <c>doc/dev/tmp/management-action-implementation-design.md</c>. The flow mirrors §4 / §5 of
    /// that design: a per-action loop that multiplexes incoming RPC requests and lifecycle
    /// notifications via <see cref="Task.WhenAny(Task, Task)"/>.
    /// </summary>
    public sealed class ManagementActionConnectorWorker : BackgroundService
    {
        private readonly ILogger<ManagementActionConnectorWorker> _logger;
        private readonly ConnectorWorker _connector;

        public ManagementActionConnectorWorker(
            ApplicationContext applicationContext,
            ILogger<ManagementActionConnectorWorker> logger,
            ILogger<ConnectorWorker> connectorLogger,
            IMqttClient mqttClient,
            IMessageSchemaProvider messageSchemaProvider,
            IAzureDeviceRegistryClientWrapperProvider adrClientFactory)
        {
            _logger = logger;
            _connector = new ConnectorWorker(
                applicationContext,
                connectorLogger,
                mqttClient,
                messageSchemaProvider,
                adrClientFactory)
            {
                WhileDeviceIsAvailable = WhileDeviceAvailableAsync,
                WhileAssetIsAvailable = WhileAssetAvailableAsync,
            };
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

            // Spawn one handler task per management action. They run concurrently for the
            // lifetime of the asset; cancelling 'cancellationToken' tears them all down.
            var actionTasks = new List<Task>();
            foreach (var group in args.Asset.ManagementGroups ?? Enumerable.Empty<AssetManagementGroup>())
            {
                foreach (var action in group.Actions ?? Enumerable.Empty<AssetManagementGroupAction>())
                {
                    actionTasks.Add(HandleManagementActionAsync(
                        args.AssetClient,
                        group.Name,
                        action.Name,
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
        /// Per-action loop: interleaves <c>RecvRequestAsync</c> and
        /// <c>RecvManagementActionNotificationAsync</c> and reacts to the four notification
        /// variants defined in the design doc.
        /// </summary>
        private async Task HandleManagementActionAsync(
            AssetClient assetClient,
            string groupName,
            string actionName,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting handler for management action {Group}::{Action}", groupName, actionName);

            // Executor may be null at any point in the action's lifetime — for example, the
            // current definition was rejected with a ConfigError, or the SDK hasn't bound a
            // CommandExecutor yet. A null executor is not an error: we just skip the request
            // half of the select-style loop and wait for the next lifecycle notification to
            // bring us a fresh one.
            ManagementActionExecutor? executor =
                await assetClient.GetManagementActionExecutorAsync(groupName, actionName, cancellationToken);

            // TODO: register request/response schemas

            while (!cancellationToken.IsCancellationRequested)
            {
                Task<ManagementActionNotification> recvNotificationTask =
                    assetClient.RecvManagementActionNotificationAsync(groupName, actionName, cancellationToken);

                if (executor is null)
                {
                    // No valid executor right now — only the notification path is live. Wait for
                    // the next definition (which may carry a fresh executor) and react to it.
                    ManagementActionNotification notification = await recvNotificationTask;
                    bool shouldExit = await HandleNotificationAsync(
                        assetClient, groupName, actionName, notification,
                        currentExecutor: null,
                        updateExecutor: next => executor = next,
                        cancellationToken);
                    if (shouldExit)
                    {
                        break;
                    }
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

                    await HandleRequestAsync(request, groupName, actionName, cancellationToken);
                }
                else
                {
                    ManagementActionNotification notification = await recvNotificationTask;
                    bool shouldExit = await HandleNotificationAsync(
                        assetClient, groupName, actionName, notification,
                        currentExecutor: executor,
                        updateExecutor: next => executor = next,
                        cancellationToken);
                    if (shouldExit)
                    {
                        break;
                    }
                }
            }

            if (executor is not null)
            {
                await executor.DisposeAsync();
            }
            _logger.LogInformation("Handler for {Group}::{Action} exited.", groupName, actionName);
        }

        private async Task HandleRequestAsync(
            ManagementActionRequest request,
            string groupName,
            string actionName,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Received invocation for {Group}::{Action} ({Bytes} bytes, content-type={ContentType})",
                groupName, actionName, request.Payload.Length, request.ContentType);

            // Minimal no-op handler: echo back an empty success response. Real connectors would
            // parse request.Payload per request.ContentType, execute on the device, and build a
            // typed response.
            var response = new ManagementActionResponse
            {
                Payload = ReadOnlySequence<byte>.Empty,
                ContentType = "application/json",
                CloudEvent = null,
            };

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
        /// Handle a lifecycle notification. Returns <c>true</c> if the per-action loop should
        /// exit (i.e. action was deleted).
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
                    // Real connectors would re-validate the new definition here. For this sample
                    // we assume it's fine and report Available + clear config error.
                    // - Config status records whether this connector accepted the new definition
                    //   (durable; surfaced to cloud via AssetStatus).
                    // - Runtime health records current liveness (telemetry; also ends the pause —
                    //   see AssetRuntimeHealthReporter: pause sets the cached entry to null and a
                    //   subsequent Report* overwrites it with a non-null value).
                    await ReportActionConfigStatusAsync(assetClient, groupName, actionName, validationError: null, cancellationToken);
                    await ReportActionAvailableAsync(assetClient, groupName, actionName, cancellationToken);
                    return false;

                case ManagementActionUpdatedWithNewExecutor updatedWithNew:
                    _logger.LogInformation(
                        "{Group}::{Action} definition updated — swapping to new executor. error={Error}",
                        groupName, actionName, updatedWithNew.Error);
                    await assetClient.PauseManagementActionRuntimeHealthReportingAsync(groupName, actionName, cancellationToken);

                    // Drain + dispose the OLD executor BEFORE swapping so callers whose
                    // requests were already queued on the old topic get an explicit
                    // "ManagementActionDefinitionOutdated" response instead of timing out.
                    // See design doc §5. Skipped if there was no prior executor (e.g. the
                    // previous definition had been rejected with a ConfigError).
                    if (currentExecutor is not null)
                    {
                        await DrainAndDisposeExecutorAsync(
                            currentExecutor, groupName, actionName,
                            errorCode: "ManagementActionDefinitionOutdated",
                            errorMessage: "Management action definition changed; this request was received on the previous topic.",
                            cancellationToken);
                    }

                    // NewExecutor may itself be null if the new definition was rejected — record
                    // that and keep waiting for a future valid definition.
                    updateExecutor(updatedWithNew.NewExecutor);

                    // Same two-channel update as the same-topic case above.
                    await ReportActionConfigStatusAsync(assetClient, groupName, actionName, validationError: null, cancellationToken);
                    await ReportActionAvailableAsync(assetClient, groupName, actionName, cancellationToken);
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
                            errorMessage: "Management action definition deleted; this request was received the definition was deleted.",
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

        /// <summary>
        /// Report the action as Available. A Report* call is also what ends a prior
        /// <see cref="AssetClient.PauseManagementActionRuntimeHealthReportingAsync"/> —
        /// there is no separate "resume" API.
        /// </summary>
        private static Task ReportActionAvailableAsync(
            AssetClient assetClient,
            string groupName,
            string actionName,
            CancellationToken cancellationToken)
            => assetClient.ReportManagementActionRuntimeHealthAsync(
                groupName,
                actionName,
                new ConnectorRuntimeHealth { Status = HealthStatus.Available },
                cancellationToken: cancellationToken);

        /// <summary>
        /// Update the durable per-action <see cref="AssetManagementGroupActionStatus.Error"/>
        /// (config status) on the asset. Pass <paramref name="validationError"/> = <c>null</c>
        /// to clear a previous error, or a populated <see cref="ConfigError"/> to record that
        /// the connector rejected the latest definition revision.
        /// </summary>
        /// <remarks>
        /// This is distinct from runtime health: config status is the durable record of
        /// "did this connector accept the action's definition?", surfaced back to the cloud
        /// via the asset's status. Runtime health is volatile telemetry about current liveness.
        /// On a definition change, both should be updated.
        /// </remarks>
        private static Task ReportActionConfigStatusAsync(
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
        /// Budget for draining an outdated executor's backlog before we give up and dispose.
        /// Chosen so a misbehaving MQTT client backlog cannot wedge the per-action loop.
        /// </summary>
        private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Pull every still-queued request from <paramref name="executor"/>, respond to each
        /// with <paramref name="errorCode"/> / <paramref name="errorMessage"/>, then dispose.
        /// Bounded by <see cref="DrainTimeout"/>.
        /// </summary>
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
                    // RecvRequestAsync returns null once the executor has no more requests
                    // (SDK has unsubscribed the old topic and the in-memory queue is empty).
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

                    if (stale is null)
                    {
                        break;
                    }

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
                // DisposeAsync is the safety net: if anything escaped the drain loop
                // (e.g. we timed out with requests still queued), the executor's own
                // Drop/Dispose semantics will auto-complete them with a generic error.
                await executor.DisposeAsync();
            }

            _logger.LogInformation(
                "{Group}::{Action}: drained {Drained} stale request(s) from old executor.",
                groupName, actionName, drained);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting ManagementActionConnector...");
            await _connector.RunConnectorAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _connector.Dispose();
            base.Dispose();
        }
    }
}

