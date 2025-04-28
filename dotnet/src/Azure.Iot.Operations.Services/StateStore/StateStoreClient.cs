// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Diagnostics;
using System.Text;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;

namespace Azure.Iot.Operations.Services.StateStore
{
    /// <summary>
    /// A client for interacting with the Akri MQ distributed state store.
    /// </summary>
    public class StateStoreClient : IAsyncDisposable, IStateStoreClient
    {
        private readonly IStateStoreClientStub? _generatedClientStub;
        private readonly IMqttPubSubClient? _mqttClient; // only used in this layer while the code gen patterns for KeyNotify type scenarios aren't solved yet.
        private bool _isSubscribedToNotifications = false;
        private const string NotificationsTopicFormat = "clients/statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/{0}/command/notify";
        private const string NotificationsTopicFilter = NotificationsTopicFormat + "/+";
        private string _clientIdHexString = "";
        private bool _disposed = false;

        internal const string FencingTokenUserPropertyKey = AkriSystemProperties.ReservedPrefix + "ft";

        public event Func<object?, KeyChangeMessageReceivedEventArgs, Task>? KeyChangeMessageReceivedAsync;

        public StateStoreClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
        {
            _generatedClientStub = new StateStoreClientStub(applicationContext, mqttClient);
            _mqttClient = mqttClient;
            _mqttClient.ApplicationMessageReceivedAsync += OnTelemetryReceived;
        }

        // For unit test purposes only
        internal StateStoreClient(IMqttPubSubClient mqttClient, IStateStoreClientStub generatedClientStub)
            : this(new ApplicationContext(), mqttClient)
        {
            _generatedClientStub = generatedClientStub;
            _mqttClient = mqttClient;
            _mqttClient.ApplicationMessageReceivedAsync += OnTelemetryReceived;
        }

        // For unit test purposes only
        internal StateStoreClient()
        {
        }

        /// <inheritdoc/>
        private async Task OnTelemetryReceived(MqttApplicationMessageReceivedEventArgs args)
        {
            string topic = args.ApplicationMessage.Topic;
            Debug.Assert(_mqttClient != null);

            // Note that the client Id is expected to be encoded as a hex string in this topic
            if (MqttTopicProcessor.DoesTopicMatchFilter(topic, string.Format(NotificationsTopicFilter, _clientIdHexString)))
            {
                HybridLogicalClock? version = null;
                if (args.ApplicationMessage == null || args.ApplicationMessage.Payload.IsEmpty)
                {
                    Trace.TraceWarning("Received a message on the key-notify topic without any payload. Ignoring it.");
                    return;
                }

                if (args.ApplicationMessage.UserProperties != null)
                {
                    foreach (MqttUserProperty userProperty in args.ApplicationMessage.UserProperties)
                    {
                        if (userProperty.Name.Equals("__ts"))
                        {
                            version = HybridLogicalClock.DecodeFromString(AkriSystemProperties.Timestamp, userProperty.Value);
                            break;
                        }
                    }
                }

                if (topic.Split('/').Length != 8)
                {
                    Trace.TraceWarning("Received a message on the key-notify topic with an unexpected topic format. Ignoring it.");
                    return;
                }

                byte[] keyBeingNotified;
                try
                {
                    string lastTopicSegment = topic.Split('/')[7];
                    keyBeingNotified = Convert.FromHexString(lastTopicSegment);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("Received a message on the key-notify topic with an unexpected topic format. Could not decode the key that was notified. Ignoring it.", ex);
                    return;
                }

                if (version == null)
                {
                    Trace.TraceWarning("Received a message on the key-notify topic without a timestamp. Ignoring it");
                    return;
                }

                StateStoreKeyNotification notification;
                try
                {
                    if (args.ApplicationMessage.Payload.IsEmpty)
                    {
                        Trace.TraceWarning("Received a message on the key-notify topic with no payload. Ignoring it.");
                        return;
                    }

                    notification = StateStorePayloadParser.ParseKeyNotification(args.ApplicationMessage.Payload.ToArray(), keyBeingNotified, version);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("Received a message on the key-notify topic with an unexpected payload format. Ignoring it.", ex);
                    return;
                }

                var keyChangeArgs = new KeyChangeMessageReceivedEventArgs(notification.Key, notification.KeyState, version)
                {
                    NewValue = notification.Value
                };
                if (KeyChangeMessageReceivedAsync != null)
                {
                    await KeyChangeMessageReceivedAsync.Invoke(this, keyChangeArgs);
                }
            }
        }

        /// <inheritdoc/>
        public virtual async Task<StateStoreGetResponse> GetAsync(StateStoreKey key, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(key, nameof(key));
            ArgumentNullException.ThrowIfNull(key.Bytes, nameof(key.Bytes));
            ObjectDisposedException.ThrowIf(_disposed, this);

            Debug.Assert(_generatedClientStub != null);

            byte[] requestPayload = StateStorePayloadParser.BuildGetRequestPayload(key);
            Trace.TraceInformation($"GET {Encoding.ASCII.GetString(key.Bytes)}");
            ExtendedResponse<byte[]> commandResponse =
                await _generatedClientStub.InvokeAsync(
                    requestPayload,
                    commandTimeout: requestTimeout,
                    cancellationToken: cancellationToken).WithMetadata();

            if (commandResponse.Response == null
                || commandResponse.Response.Length == 0)
            {
                throw new StateStoreOperationException("Received no response payload from State Store");
            }

            byte[]? value = StateStorePayloadParser.ParseGetResponse(commandResponse.Response);

            if (value == null)
            {
                // This case signifies that the requested key did not exist in the state store. Note that
                // this is not the same as the case where the requested key exists and the value is an empty
                // byte array. In the latter case, there may still be a timestamp attached to the key.
                return new StateStoreGetResponse(null, null);
            }

            return new StateStoreGetResponse(
                commandResponse.ResponseMetadata != null ? commandResponse.ResponseMetadata.Timestamp! : null,
                new StateStoreValue(value));
        }

        /// <inheritdoc/>
        public virtual async Task<StateStoreSetResponse> SetAsync(StateStoreKey key, StateStoreValue value, StateStoreSetRequestOptions? options = null, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(key, nameof(key));
            ArgumentNullException.ThrowIfNull(value, nameof(value));
            ArgumentNullException.ThrowIfNull(key.Bytes, nameof(key.Bytes));
            ArgumentNullException.ThrowIfNull(value.Bytes, nameof(value.Bytes));
            ObjectDisposedException.ThrowIf(_disposed, this);

            Debug.Assert(_generatedClientStub != null);

            options ??= new StateStoreSetRequestOptions();

            byte[] requestPayload = StateStorePayloadParser.BuildSetRequestPayload(key, value, options);
            Trace.TraceInformation($"SET {Encoding.ASCII.GetString(key.Bytes)}");

            CommandRequestMetadata requestMetadata = new CommandRequestMetadata();
            if (options.FencingToken != null)
            {
                requestMetadata.UserData.TryAdd(FencingTokenUserPropertyKey, options.FencingToken.EncodeToString());
            }

            ExtendedResponse<byte[]> commandResponse =
                await _generatedClientStub.InvokeAsync(
                    requestPayload,
                    requestMetadata,
                    commandTimeout: requestTimeout,
                    cancellationToken: cancellationToken).WithMetadata();

            if (commandResponse.Response == null
                            || commandResponse.Response.Length == 0)
            {
                throw new StateStoreOperationException("Received no response payload from State Store");
            }

            return StateStorePayloadParser.ParseSetResponse(commandResponse.Response, commandResponse.ResponseMetadata?.Timestamp);
        }

        /// <inheritdoc/>
        public virtual async Task<StateStoreDeleteResponse> DeleteAsync(StateStoreKey key, StateStoreDeleteRequestOptions? options = null, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ArgumentNullException.ThrowIfNull(key, nameof(key));
            ArgumentNullException.ThrowIfNull(key.Bytes, nameof(key.Bytes));
            ObjectDisposedException.ThrowIf(_disposed, this);

            Debug.Assert(_generatedClientStub != null);

            options ??= new StateStoreDeleteRequestOptions();

            byte[] requestPayload = options.OnlyDeleteIfValueEquals != null
                ? StateStorePayloadParser.BuildVDelRequestPayload(key, options.OnlyDeleteIfValueEquals)
                : StateStorePayloadParser.BuildDelRequestPayload(key);
            Trace.TraceInformation($"DEL {Encoding.ASCII.GetString(key.Bytes)}");

            CommandRequestMetadata requestMetadata = new CommandRequestMetadata();
            if (options.FencingToken != null)
            {
                requestMetadata.UserData.TryAdd(FencingTokenUserPropertyKey, options.FencingToken.EncodeToString());
            }

            ExtendedResponse<byte[]> commandResponse =
                await _generatedClientStub.InvokeAsync(
                    requestPayload,
                    requestMetadata,
                    commandTimeout: requestTimeout,
                    cancellationToken: cancellationToken).WithMetadata();

            if (commandResponse.Response == null
                || commandResponse.Response.Length == 0)
            {
                throw new StateStoreOperationException("Received no response payload from State Store");
            }

            return new StateStoreDeleteResponse(StateStorePayloadParser.ParseDelResponse(commandResponse.Response));
        }

        /// <inheritdoc/>
        public virtual async Task ObserveAsync(StateStoreKey key, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ArgumentNullException.ThrowIfNull(key, nameof(key));
            ArgumentNullException.ThrowIfNull(key.Bytes, nameof(key.Bytes));

            ObjectDisposedException.ThrowIf(_disposed, this);

            Debug.Assert(_generatedClientStub != null);
            Debug.Assert(_mqttClient != null);

            if (!_isSubscribedToNotifications)
            {
                string? clientId = _mqttClient.ClientId;
                Debug.Assert(!string.IsNullOrEmpty(clientId));

                // When receiving notifications, the topic string will contain the hex string encoded client id.
                // Since that value doesn't change from notification to notification, calculate that hex 
                // string once here instead of calculating it each time a notification is received.
                byte[] clientIdBytes = Encoding.UTF8.GetBytes(clientId);
                _clientIdHexString = Convert.ToHexString(clientIdBytes);

                MqttClientSubscribeOptions subscribeOptions = new MqttClientSubscribeOptions(string.Format(NotificationsTopicFormat, _mqttClient.ClientId), MqttQualityOfServiceLevel.AtLeastOnce);

                MqttClientSubscribeResult subscribeResult = await _mqttClient.SubscribeAsync(subscribeOptions, cancellationToken).ConfigureAwait(false);
                if (subscribeResult.Items.First().ReasonCode != MqttClientSubscribeReasonCode.GrantedQoS1)
                {
                    throw new StateStoreOperationException("failed to subscribe to state store notifications");
                }

                _isSubscribedToNotifications = true;
                Trace.TraceInformation($"Subscribed to key notifications for this client");
            }

            Trace.TraceInformation($"OBSERVE {Encoding.ASCII.GetString(key.Bytes)}");
            byte[] requestPayload = StateStorePayloadParser.BuildKeyNotifyRequestPayload(key);
            ExtendedResponse<byte[]> commandResponse =
                await _generatedClientStub.InvokeAsync(
                    requestPayload,
                    commandTimeout: requestTimeout,
                    cancellationToken: cancellationToken).WithMetadata();

            if (commandResponse.Response == null || commandResponse.Response.Length == 0)
            {
                throw new StateStoreOperationException("Received no response payload from State Store");
            }

            StateStorePayloadParser.ValidateKeyNotifyResponse(commandResponse.Response);
        }

        /// <inheritdoc/>
        public async virtual Task UnobserveAsync(StateStoreKey key, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            ArgumentNullException.ThrowIfNull(key, nameof(key));
            ArgumentNullException.ThrowIfNull(key.Bytes, nameof(key.Bytes));

            ObjectDisposedException.ThrowIf(_disposed, this);

            Debug.Assert(_generatedClientStub != null);

            Trace.TraceInformation($"UNOBSERVE {Encoding.ASCII.GetString(key.Bytes)}");
            byte[] requestPayload = StateStorePayloadParser.BuildKeyNotifyStopRequestPayload(key);
            ExtendedResponse<byte[]> commandResponse =
                await _generatedClientStub.InvokeAsync(
                    requestPayload,
                    commandTimeout: requestTimeout,
                    cancellationToken: cancellationToken).WithMetadata();

            if (commandResponse.Response == null || commandResponse.Response.Length == 0)
            {
                throw new StateStoreOperationException("Received no response payload from State Store");
            }

            StateStorePayloadParser.ValidateKeyNotifyResponse(commandResponse.Response);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore(false).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync(bool disposing)
        {
            await DisposeAsyncCore(disposing).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        protected async virtual ValueTask DisposeAsyncCore(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (_generatedClientStub != null && _mqttClient != null)
            {
                if (_isSubscribedToNotifications
                    && !string.IsNullOrEmpty(_mqttClient.ClientId))
                {
                    MqttClientUnsubscribeOptions unsubscribeOptions = new MqttClientUnsubscribeOptions(string.Format(NotificationsTopicFormat, _mqttClient.ClientId));

                    try
                    {
                        await _mqttClient.UnsubscribeAsync(unsubscribeOptions).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        // Not that big of a problem. Also a temporary responsibility of this layer while the code-gen for key notify pattern
                        // is unfinished. Once it is finished, this layer won't be responsible for managing MQTT topic subscriptions.
                        Trace.TraceWarning("Failed to unsubscribe from key notifications MQTT topic.", e);
                    }
                }

                _mqttClient.ApplicationMessageReceivedAsync -= OnTelemetryReceived;
                await _generatedClientStub.DisposeAsync().ConfigureAwait(false);

                if (disposing)
                {
                    await _mqttClient.DisposeAsync(disposing);
                }
            }

            _disposed = true;
        }
    }
}
