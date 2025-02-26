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
        private readonly StateStoreGeneratedClientHolder? _generatedClientHolder;
        private readonly IMqttPubSubClient? _mqttClient; // only used in this layer while the code gen patterns for KeyNotify type scenarios aren't solved yet.
        private bool _isSubscribedToNotifications = false;
        private const string NotificationsTopicFormat = "clients/statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/{0}/command/notify";
        private const string NotificationsTopicFilter = NotificationsTopicFormat + "/+";
        string _clientIdHexString = "";
        private bool _disposed = false;
        private readonly ApplicationContext _applicationContext;

        internal const string FencingTokenUserPropertyKey = AkriSystemProperties.ReservedPrefix + "ft";

        public event Func<object?, KeyChangeMessageReceivedEventArgs, Task>? KeyChangeMessageReceivedAsync;

        public StateStoreClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
        {
            _applicationContext = applicationContext;
            _generatedClientHolder = new StateStoreGeneratedClientHolder(new StateStoreGeneratedClient(applicationContext, mqttClient));
            _mqttClient = mqttClient;
            _mqttClient.ApplicationMessageReceivedAsync += OnTelemetryReceived;
        }

        // For unit test purposes only
        internal StateStoreClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, StateStoreGeneratedClientHolder generatedClientHolder)
            : this(new ApplicationContext(), mqttClient)
        {
            _applicationContext = applicationContext;
            _generatedClientHolder = generatedClientHolder;
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
                            version = DecodeFromString(userProperty.Value);
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

                var keyChangeArgs = new KeyChangeMessageReceivedEventArgs(notification.Key, notification.KeyState, version);
                keyChangeArgs.NewValue = notification.Value;
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

            Debug.Assert(_generatedClientHolder != null);

            byte[] requestPayload = StateStorePayloadParser.BuildGetRequestPayload(key);
            LogWithoutLineBreaks($"-> {Encoding.ASCII.GetString(requestPayload)}");
            ExtendedResponse<byte[]> commandResponse =
                await _generatedClientHolder.InvokeAsync(
                    requestPayload,
                    commandTimeout: requestTimeout,
                    cancellationToken: cancellationToken).WithMetadata();

            if (commandResponse.Response == null
                || commandResponse.Response.Length == 0)
            {
                throw new StateStoreOperationException("Received no response payload from State Store");
            }

            byte[]? value = StateStorePayloadParser.ParseGetResponse(commandResponse.Response);

            LogWithoutLineBreaks($"<- {Encoding.ASCII.GetString(commandResponse.Response)}");

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

            Debug.Assert(_generatedClientHolder != null);

            options ??= new StateStoreSetRequestOptions();

            byte[] requestPayload = StateStorePayloadParser.BuildSetRequestPayload(key, value, options);
            LogWithoutLineBreaks($"-> {Encoding.ASCII.GetString(requestPayload)}");

            CommandRequestMetadata requestMetadata = new CommandRequestMetadata();
            if (options.FencingToken != null)
            {
                requestMetadata.UserData.TryAdd(FencingTokenUserPropertyKey, options.FencingToken.EncodeToString());
            }

            ExtendedResponse<byte[]> commandResponse =
                await _generatedClientHolder.InvokeAsync(
                    requestPayload,
                    requestMetadata,
                    commandTimeout: requestTimeout,
                    cancellationToken: cancellationToken).WithMetadata();

            if (commandResponse.Response == null
                            || commandResponse.Response.Length == 0)
            {
                throw new StateStoreOperationException("Received no response payload from State Store");
            }

            LogWithoutLineBreaks($"<- {Encoding.ASCII.GetString(commandResponse.Response)}");

            return StateStorePayloadParser.ParseSetResponse(commandResponse.Response, commandResponse.ResponseMetadata?.Timestamp);
        }

        /// <inheritdoc/>
        public virtual async Task<StateStoreDeleteResponse> DeleteAsync(StateStoreKey key, StateStoreDeleteRequestOptions? options = null, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ArgumentNullException.ThrowIfNull(key, nameof(key));
            ArgumentNullException.ThrowIfNull(key.Bytes, nameof(key.Bytes));
            ObjectDisposedException.ThrowIf(_disposed, this);

            Debug.Assert(_generatedClientHolder != null);

            options ??= new StateStoreDeleteRequestOptions();

            byte[] requestPayload;
            if (options.OnlyDeleteIfValueEquals != null)
            {
                requestPayload = StateStorePayloadParser.BuildVDelRequestPayload(key, options.OnlyDeleteIfValueEquals);
            }
            else
            {
                requestPayload = StateStorePayloadParser.BuildDelRequestPayload(key);
            }

            LogWithoutLineBreaks($"-> {Encoding.ASCII.GetString(requestPayload)}");

            CommandRequestMetadata requestMetadata = new CommandRequestMetadata();
            if (options.FencingToken != null)
            {
                requestMetadata.UserData.TryAdd(FencingTokenUserPropertyKey, options.FencingToken.EncodeToString());
            }

            ExtendedResponse<byte[]> commandResponse =
                await _generatedClientHolder.InvokeAsync(
                    requestPayload,
                    requestMetadata,
                    commandTimeout: requestTimeout,
                    cancellationToken: cancellationToken).WithMetadata();

            if (commandResponse.Response == null
                || commandResponse.Response.Length == 0)
            {
                throw new StateStoreOperationException("Received no response payload from State Store");
            }

            LogWithoutLineBreaks($"<- {Encoding.ASCII.GetString(commandResponse.Response)}");
            return new StateStoreDeleteResponse(StateStorePayloadParser.ParseDelResponse(commandResponse.Response));
        }

        /// <inheritdoc/>
        public virtual async Task ObserveAsync(StateStoreKey key, StateStoreObserveRequestOptions? options = null, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ArgumentNullException.ThrowIfNull(key, nameof(key));
            ArgumentNullException.ThrowIfNull(key.Bytes, nameof(key.Bytes));

            ObjectDisposedException.ThrowIf(_disposed, this);

            Debug.Assert(_generatedClientHolder != null);
            Debug.Assert(_mqttClient != null);

            //TODO these values are ignored because the service currently only allows one configuration. Once
            // service support is added for these flags, we can respect the options here.
            options ??= new StateStoreObserveRequestOptions();

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
                Trace.TraceInformation($"Subscribed to notifications for key {key}");
            }

            byte[] requestPayload = StateStorePayloadParser.BuildKeyNotifyRequestPayload(key);
            ExtendedResponse<byte[]> commandResponse =
                await _generatedClientHolder.InvokeAsync(
                    requestPayload,
                    commandTimeout: requestTimeout,
                    cancellationToken: cancellationToken).WithMetadata();

            Trace.TraceInformation($"Key notification receiver started for key {key}.");
            Trace.TraceInformation($"Response from Observe Async: {Encoding.ASCII.GetString(commandResponse.Response)}");

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

            Debug.Assert(_generatedClientHolder != null);

            byte[] requestPayload = StateStorePayloadParser.BuildKeyNotifyStopRequestPayload(key);
            ExtendedResponse<byte[]> commandResponse =
                await _generatedClientHolder.InvokeAsync(
                    requestPayload,
                    commandTimeout: requestTimeout,
                    cancellationToken: cancellationToken).WithMetadata();

            Trace.TraceInformation($"Key notification receiver stopped for key {key}.");
            Trace.TraceInformation($"Response from Un-observe Async: {Encoding.ASCII.GetString(commandResponse.Response)}");

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

            if (_generatedClientHolder != null && _mqttClient != null)
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
                await _generatedClientHolder.DisposeAsync().ConfigureAwait(false);

                if (disposing)
                {
                    await _mqttClient.DisposeAsync(disposing);
                }
            }

            _disposed = true;
        }

        private void LogWithoutLineBreaks(string message)
        {
            // Escape the \r\n characters so they don't actually print new lines in the logger
            Trace.TraceInformation(message.Replace("\r\n", "\\r\\n"));
        }

        //TODO this code is temporary while the telemetry receiver pattern is implemented in code gen. Once it is implemented
        // in code gen, this should be handled by the underlying library and this block can be deleted.
        internal static HybridLogicalClock DecodeFromString(string encoded)
        {
            string[] array = encoded.Split(":");
            if (array.Length != 3)
            {
                throw new HybridLogicalClockException("Malformed HLC. Expected three segments separated by ':' character");
            }

            DateTime unixEpoch = DateTime.UnixEpoch;
            if (double.TryParse(array[0], out var result))
            {
                unixEpoch = unixEpoch.AddMilliseconds(result);
                int counter;
                try
                {
                    counter = Convert.ToInt32(array[1], 10);
                }
                catch (Exception)
                {
                    throw new HybridLogicalClockException("Malformed HLC. Could not parse second segment as a base 32 integer");
                }

                if (array[2].Length < 1)
                {
                    throw new HybridLogicalClockException("Malformed HLC. Missing nodeId as the final segment");
                }

                string nodeId = array[2];
                return new HybridLogicalClock(unixEpoch, counter, nodeId);
            }

            throw new HybridLogicalClockException("Malformed HLC. Could not parse first segment as an integer");
        }
    }
}
