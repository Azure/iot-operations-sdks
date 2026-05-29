// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Executor for a single management action. Thin wrapper over the MQTT RPC command
    /// executor (<c>CommandExecutor&lt;BypassPayload, BypassPayload&gt;</c>) subscribed to the
    /// action's request topic. Obtain instances via
    /// <see cref="AssetClient.GetManagementActionExecutorAsync(string, string, System.Threading.CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The executor is callback-shaped, mirroring the underlying
    /// <c>CommandExecutor.OnCommandReceived</c> contract: set
    /// <see cref="OnRequestReceived"/> once after acquiring the executor (or after a swap
    /// triggered by a <see cref="ManagementActionUpdatedWithNewExecutor"/> notification),
    /// and the callback will be invoked once per incoming management action request.
    /// The returned <see cref="ManagementActionResponse"/> is sent back to the invoker.
    /// </para>
    /// <para>
    /// Exceptions thrown by the callback are surfaced to the invoker as
    /// <see cref="ManagementActionApplicationError"/> responses by the connector worker; see
    /// <see cref="ConnectorWorker"/>.
    /// </para>
    /// </remarks>
    internal sealed class ManagementActionExecutor : IAsyncDisposable
    {
        private const string DefaultContentType = "application/octet-stream";

        private readonly InnerCommandExecutor _inner;
        private readonly string _deviceName;
        private readonly string _assetName;
        private readonly string _groupName;
        private readonly string _actionName;
        private readonly AssetManagementGroupActionType _actionType;

        /// <summary>
        /// Creates a new executor for a single management action. Intended to be invoked by
        /// <see cref="AssetClient"/>; not part of the public API surface.
        /// </summary>
        /// <param name="applicationContext">Shared application context (HLC, etc.).</param>
        /// <param name="mqttClient">Shared MQTT pub/sub client.</param>
        /// <param name="deviceName">Device that owns the asset.</param>
        /// <param name="assetName">Asset that declares the management group.</param>
        /// <param name="groupName">Management group name.</param>
        /// <param name="actionName">Management action name (also used as the underlying command name).</param>
        /// <param name="actionType">Static action type (Call/Read/Write).</param>
        /// <param name="requestTopicPattern">Resolved request topic pattern for the action.</param>
        /// <param name="serviceGroupId">
        /// MQTT5 shared-subscription group id. Empty string means "no shared subscription".
        /// Source of this value is an open design question (see
        /// <c>doc/dev/tmp/management-action-implementation-design.md</c>, Open Questions §1);
        /// callers should pass an explicit value.
        /// </param>
        /// <param name="executionTimeout">Per-invocation execution timeout.</param>
        /// <param name="topicTokenMap">
        /// Token replacements applied when resolving <paramref name="requestTopicPattern"/>.
        /// Typically contains device, endpoint, asset, group, and action names.
        /// </param>
        internal ManagementActionExecutor(
            ApplicationContext applicationContext,
            IMqttPubSubClient mqttClient,
            string deviceName,
            string assetName,
            string groupName,
            string actionName,
            AssetManagementGroupActionType actionType,
            string requestTopicPattern,
            string serviceGroupId,
            TimeSpan executionTimeout,
            IReadOnlyDictionary<string, string> topicTokenMap)
        {
            ArgumentNullException.ThrowIfNull(applicationContext);
            ArgumentNullException.ThrowIfNull(mqttClient);
            ArgumentException.ThrowIfNullOrEmpty(deviceName);
            ArgumentException.ThrowIfNullOrEmpty(assetName);
            ArgumentException.ThrowIfNullOrEmpty(groupName);
            ArgumentException.ThrowIfNullOrEmpty(actionName);
            ArgumentException.ThrowIfNullOrEmpty(requestTopicPattern);
            ArgumentNullException.ThrowIfNull(serviceGroupId);
            ArgumentNullException.ThrowIfNull(topicTokenMap);

            _deviceName = deviceName;
            _assetName = assetName;
            _groupName = groupName;
            _actionName = actionName;
            _actionType = actionType;

            _inner = new InnerCommandExecutor(applicationContext, mqttClient, actionName)
            {
                RequestTopicPattern = requestTopicPattern,
                ServiceGroupId = serviceGroupId,
                ExecutionTimeout = executionTimeout,
                OnCommandReceived = HandleCommandAsync,
            };

            foreach (KeyValuePair<string, string> kvp in topicTokenMap)
            {
                _inner.TopicTokenMap[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Invoked once per management action request. The returned
        /// <see cref="ManagementActionResponse"/> is sent to the invoker.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Must be set before the executor begins dispatching. Requests that arrive while
        /// this property is <c>null</c> are replied to with an
        /// <see cref="ManagementActionApplicationError"/> (<c>HandlerNotConfigured</c>) so
        /// invokers see a deterministic failure rather than a timeout.
        /// </para>
        /// <para>
        /// The <see cref="ManagementActionInvokedEventArgs"/> passed to the callback is
        /// already stamped with the action's group / action / asset / device names &mdash;
        /// the executor has that context from
        /// <see cref="AssetClient.GetManagementActionExecutorAsync(string, string, CancellationToken)"/>.
        /// </para>
        /// <para>
        /// The supplied <see cref="CancellationToken"/> is signalled when the underlying
        /// command execution times out (per the command executor's <c>ExecutionTimeout</c>),
        /// when the executor is being stopped/replaced, or when the asset becomes unavailable.
        /// Handlers should honor it and abort device I/O promptly.
        /// </para>
        /// </remarks>
        public Func<ManagementActionInvokedEventArgs, CancellationToken, Task<ManagementActionResponse>>? OnRequestReceived { get; set; }

        /// <summary>
        /// Subscribe to the action's request topic and begin dispatching invocations.
        /// Intended to be invoked by <see cref="AssetClient"/> after the executor has been
        /// fully wired (i.e. after <see cref="OnRequestReceived"/> has been set, where
        /// possible) and before the orchestrator's per-action loop begins suspending on
        /// notifications.
        /// </summary>
        /// <remarks>
        /// Requests that arrive before <see cref="OnRequestReceived"/> is set are replied to
        /// with a <c>HandlerNotConfigured</c> application error.
        /// </remarks>
        internal Task StartAsync(CancellationToken cancellationToken = default)
            => _inner.StartAsync(preferredDispatchConcurrency: null, cancellationToken);

        /// <summary>
        /// Tear down the MQTT subscription for this action's request topic. After this
        /// completes the broker delivers no further requests to this executor; any
        /// in-flight <see cref="OnRequestReceived"/> invocations continue until they
        /// return (bounded by the underlying command executor's execution timeout) or
        /// are awaited out by <see cref="DisposeAsync"/>.
        /// </summary>
        /// <remarks>
        /// Intended to be invoked by the Connector SDK itself when this executor becomes
        /// outdated (action deleted, definition replaced with a new topic, asset
        /// unavailable, connector shutting down). User code generally should not call
        /// this directly; await the corresponding
        /// <see cref="ManagementActionNotification"/> and let the worker drive the
        /// lifecycle.
        /// </remarks>
        public async ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            await _inner.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Wait for any in-flight <see cref="OnRequestReceived"/> invocations to complete
        /// and release local resources owned by this executor (callback registrations,
        /// internal queues). Does not unsubscribe from MQTT &mdash; that is
        /// <see cref="StopAsync"/>'s job and must already have happened by the time the
        /// caller disposes. Does not dispose the underlying MQTT client (shared with the
        /// rest of the connector).
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            OnRequestReceived = null;
            await _inner.DisposeAsync().ConfigureAwait(false);
        }

        private async Task<ExtendedResponse<BypassPayload>> HandleCommandAsync(
            ExtendedRequest<BypassPayload> request,
            CancellationToken cancellationToken)
        {
            Func<ManagementActionInvokedEventArgs, CancellationToken, Task<ManagementActionResponse>>? handler = OnRequestReceived;
            if (handler is null)
            {
                return new ExtendedResponse<BypassPayload>
                {
                    Response = new BypassPayload(),
                    ResponseMetadata = new CommandResponseMetadata(),
                }.WithApplicationError(
                    "HandlerNotConfigured",
                    $"No OnRequestReceived handler is wired for management action '{_groupName}::{_actionName}'.");
            }

            CommandRequestMetadata requestMetadata = request.RequestMetadata;
            BypassPayload requestPayload = request.Request ?? new BypassPayload();

            ManagementActionInvokedEventArgs args = new()
            {
                DeviceName = _deviceName,
                AssetName = _assetName,
                GroupName = _groupName,
                ActionName = _actionName,
                ActionType = _actionType,
                Payload = requestPayload.Payload,
                ContentType = requestMetadata.ContentType ?? DefaultContentType,
                FormatIndicator = requestMetadata.PayloadFormatIndicator,
                CustomUserData = requestMetadata.UserData,
                TopicTokens = requestMetadata.TopicTokens,
                Timestamp = requestMetadata.Timestamp,
                InvokerId = requestMetadata.InvokerClientId,
            };

            ManagementActionResponse response = await handler(args, cancellationToken).ConfigureAwait(false);

            BypassPayload responsePayload = new()
            {
                Payload = response.Payload,
                ContentType = response.ContentType,
                FormatIndicator = response.FormatIndicator,
            };

            CommandResponseMetadata responseMetadata = new()
            {
                CloudEvent = response.CloudEvent,
            };

            if (response.CustomUserData is not null)
            {
                foreach (KeyValuePair<string, string> kvp in response.CustomUserData)
                {
                    responseMetadata.UserData[kvp.Key] = kvp.Value;
                }
            }

            ExtendedResponse<BypassPayload> extended = new()
            {
                Response = responsePayload,
                ResponseMetadata = responseMetadata,
            };

            if (response.ApplicationError is not null)
            {
                extended = extended.WithApplicationError(
                    response.ApplicationError.ErrorCode,
                    response.ApplicationError.ErrorPayload);
            }

            return extended;
        }

        /// <summary>
        /// Concrete <see cref="CommandExecutor{TReq, TResp}"/> wired to a <see cref="BypassPayload"/>
        /// serializer. The outer <see cref="ManagementActionExecutor"/> owns this instance
        /// and dispatches incoming requests through <see cref="HandleCommandAsync"/>.
        /// </summary>
        private sealed class InnerCommandExecutor : CommandExecutor<BypassPayload, BypassPayload>
        {
            public InnerCommandExecutor(
                ApplicationContext applicationContext,
                IMqttPubSubClient mqttClient,
                string commandName)
                : base(applicationContext, mqttClient, commandName, new BypassSerializer())
            {
            }
        }
    }
}

