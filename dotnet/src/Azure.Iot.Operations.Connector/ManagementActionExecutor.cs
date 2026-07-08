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
    /// Executor for a single management action. Thin wrapper over the MQTT RPC command executor
    /// (<c>CommandExecutor&lt;BypassPayload, BypassPayload&gt;</c>) subscribed to the action's request
    /// topic. Obtain instances via
    /// <see cref="AssetClient.GetManagementActionExecutorAsync(string, string, System.Threading.CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// Callback-shaped: set <see cref="OnRequestReceived"/> after acquiring the executor (or after a
    /// <see cref="ManagementActionUpdatedWithNewExecutor"/> swap); it is invoked once per request and its
    /// <see cref="ManagementActionResponse"/> is returned to the invoker. Callback exceptions are surfaced
    /// to the invoker as <see cref="ManagementActionApplicationError"/> responses by the connector worker.
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
        /// Creates a new executor for a single management action. Invoked by <see cref="AssetClient"/>;
        /// not part of the public API surface.
        /// </summary>
        /// <param name="applicationContext">Shared application context (HLC, etc.).</param>
        /// <param name="mqttClient">Shared MQTT pub/sub client.</param>
        /// <param name="deviceName">Device that owns the asset.</param>
        /// <param name="assetName">Asset that declares the management group.</param>
        /// <param name="groupName">Management group name.</param>
        /// <param name="actionName">Management action name (also used as the underlying command name).</param>
        /// <param name="actionType">Static action type (Call/Read/Write).</param>
        /// <param name="requestTopicPattern">Resolved request topic pattern for the action.</param>
        /// <param name="serviceGroupId">MQTT5 shared-subscription group id; empty means no shared subscription.</param>
        /// <param name="executionTimeout">Per-invocation execution timeout.</param>
        /// <param name="topicTokenMap">Token replacements applied when resolving <paramref name="requestTopicPattern"/>.</param>
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
        /// Invoked once per management action request; the returned <see cref="ManagementActionResponse"/>
        /// is sent to the invoker. The supplied <see cref="ManagementActionInvokedEventArgs"/> is already
        /// stamped with group/action/asset/device names.
        /// </summary>
        /// <remarks>
        /// Set this before dispatching. Requests arriving while it is <c>null</c> get a deterministic
        /// <c>HandlerNotConfigured</c> <see cref="ManagementActionApplicationError"/>. The
        /// <see cref="CancellationToken"/> fires on execution timeout, stop/replace, or asset-unavailable;
        /// handlers should honor it and abort device I/O promptly.
        /// </remarks>
        public Func<ManagementActionInvokedEventArgs, CancellationToken, Task<ManagementActionResponse>>? OnRequestReceived { get; set; }

        /// <summary>
        /// Subscribe to the action's request topic and begin dispatching. Invoked by
        /// <see cref="AssetClient"/> after the executor is wired. Requests arriving before
        /// <see cref="OnRequestReceived"/> is set get a <c>HandlerNotConfigured</c> error.
        /// </summary>
        internal Task StartAsync(CancellationToken cancellationToken = default)
            => _inner.StartAsync(preferredDispatchConcurrency: null, cancellationToken);

        /// <summary>
        /// Tear down the MQTT subscription for this action's request topic. After this completes the broker
        /// delivers no further requests; in-flight invocations continue until they return (bounded by the
        /// command executor's execution timeout). Driven by the SDK when the executor becomes outdated;
        /// user code should not call it directly.
        /// </summary>
        public async ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            await _inner.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Wait for in-flight invocations to complete and release this executor's local resources.
        /// Does not unsubscribe from MQTT (that is <see cref="StopAsync"/>'s job and must already have run)
        /// and does not dispose the shared MQTT client.
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

