// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public abstract class CommandExecutor<TReq, TResp> : IAsyncDisposable
        where TReq : class
        where TResp : class
    {
        private readonly int[] _supportedMajorProtocolVersions = [CommandVersion.MajorProtocolVersion];

        private static readonly TimeSpan DefaultExecutorTimeout = TimeSpan.FromSeconds(10);

        internal static IWallClock WallClock = new WallClock();

        private readonly IMqttPubSubClient _mqttClient;
        private readonly string _commandName;
        private readonly IPayloadSerializer _serializer;

        private readonly Dictionary<string, string> _topicTokenMap = [];

        //private readonly HybridLogicalClock hybridLogicalClock;
        private readonly ApplicationContext _applicationContext;
        private readonly ICommandResponseCache _commandResponseCache;
        private Dispatcher? _dispatcher;
        private bool _isRunning;
        private bool _hasSubscribed;
        private string _subscriptionTopic;

        private bool _isDisposed;

        public TimeSpan ExecutionTimeout { get; set; }

        public required Func<ExtendedRequest<TReq>, CancellationToken, Task<ExtendedResponse<TResp>>> OnCommandReceived { get; set; }

        public string? ExecutorId { get; init; }

        public string ServiceGroupId { get; init; }

        public string RequestTopicPattern { get; init; }

        public string? TopicNamespace { get; set; }

        public bool IsIdempotent { get; init; }

        /// <summary>
        /// The cache time-to-live that will be used for reusing a previously computed response for duplicate or equivalent idempotent command requests.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Idempotent commands will cache the response of duplicate requests until the maximum of cache time-to-live or the command request expiration.
        /// Idempotent commands will cache the response of equivalent requests until the cache time-to-live.
        /// The cache could be cleared before the time-to-live expires if the cost-weighted benefit is too low and the cache is under size pressure.
        /// </para>
        /// Two requests are considered to be duplicate when the requests have identical correlation ID.
        /// Two requests are considered to be equivalent when they have the same payload, parameters and topic, but different correlation ID.
        /// </remarks>
        public TimeSpan CacheTtl { get; init; }

        /// <summary>
        /// Gets a dictionary for adding token keys and their replacement strings, which will be substituted in request and response topic patterns.
        /// Can be overridden by a derived class, enabling the key/value pairs to be augmented and/or combined with other key/value pairs.
        /// </summary>
        public virtual Dictionary<string, string> TopicTokenMap => _topicTokenMap;

        /// <summary>
        /// Gets a dictionary used by this class's code for substituting tokens in request and response topic patterns.
        /// Can be overridden by a derived class, enabling the key/value pairs to be augmented and/or combined with other key/value pairs.
        /// </summary>
        protected virtual IReadOnlyDictionary<string, string> EffectiveTopicTokenMap => _topicTokenMap;

        public CommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer serializer)
        {
            if (commandName == null || commandName == string.Empty)
            {
                throw AkriMqttException.GetConfigurationInvalidException(nameof(commandName), string.Empty);
            }
            _applicationContext = applicationContext;
            _mqttClient = mqttClient ?? throw AkriMqttException.GetArgumentInvalidException(commandName, nameof(mqttClient), string.Empty);
            _commandName = commandName;
            _serializer = serializer ?? throw AkriMqttException.GetArgumentInvalidException(commandName, nameof(serializer), string.Empty);

            _isRunning = false;
            _hasSubscribed = false;
            _subscriptionTopic = string.Empty;

            ExecutionTimeout = DefaultExecutorTimeout;

            _commandResponseCache = CommandResponseCache.GetCache();

            _dispatcher = null;

            ExecutorId = null;
            ServiceGroupId = AttributeRetriever.GetAttribute<ServiceGroupIdAttribute>(this)?.Id ?? string.Empty;
            RequestTopicPattern = AttributeRetriever.GetAttribute<CommandTopicAttribute>(this)?.RequestTopic ?? string.Empty;
            IsIdempotent = AttributeRetriever.GetAttribute<CommandBehaviorAttribute>(this)?.IsIdempotent ?? false;
            CacheTtl = XmlConvert.ToTimeSpan(AttributeRetriever.GetAttribute<CommandBehaviorAttribute>(this)?.CacheTtl ?? "PT0H0M0S");

            mqttClient.ApplicationMessageReceivedAsync += MessageReceivedCallbackAsync;
        }

        private async Task MessageReceivedCallbackAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            string requestTopicFilter = GetCommandTopic(null);

            if (MqttTopicProcessor.DoesTopicMatchFilter(args.ApplicationMessage.Topic, requestTopicFilter))
            {
                args.AutoAcknowledge = false;

                DateTime messageReceivedTime = WallClock.UtcNow;

                // MessageExpiryInterval is required; if it is missing, this.ExecutionTimeout is substituted as a fail-safe value when sending the error response.
                TimeSpan commandTimeout = args.ApplicationMessage.MessageExpiryInterval != default ? TimeSpan.FromSeconds(args.ApplicationMessage.MessageExpiryInterval) : ExecutionTimeout;
                DateTime commandExpirationTime = messageReceivedTime + commandTimeout;
                DateTime ttl = messageReceivedTime + CacheTtl;

                string? requestedProtocolVersion = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.ProtocolVersion)?.Value;
                if (!TryValidateRequestHeaders(args.ApplicationMessage, out CommandStatusCode? status, out string? statusMessage, out string? invalidPropertyName, out string? invalidPropertyValue))
                {
                    Trace.TraceWarning($"Command '{_commandName}' header validation failed. Status message: {statusMessage}");

                    await GetDispatcher()(
                        status != null ? async () => { await GenerateAndPublishResponseAsync(commandExpirationTime, args.ApplicationMessage.ResponseTopic!, args.ApplicationMessage.CorrelationData!, (CommandStatusCode)status, statusMessage, null, null, false, invalidPropertyName, invalidPropertyValue, requestedProtocolVersion).ConfigureAwait(false); }
                    : null,
                        async () => { await args.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
                    return;
                }

                // This validation is handled above, so assume a response topic is provided beyond this point.
                Debug.Assert(args.ApplicationMessage.ResponseTopic != null);
                Debug.Assert(args.ApplicationMessage.CorrelationData != null);

                string? clientId = _mqttClient.ClientId;
                Debug.Assert(!string.IsNullOrEmpty(clientId));
                string executorId = ExecutorId ?? clientId;
                bool isExecutorSpecific = args.ApplicationMessage.Topic.Contains(executorId);
                string sourceId = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.SourceId)?.Value ?? string.Empty;

                Task<MqttApplicationMessage>? cachedResponse =
                    await _commandResponseCache.RetrieveAsync(
                        _commandName,
                        sourceId,
                        args.ApplicationMessage.ResponseTopic,
                        args.ApplicationMessage.CorrelationData,
                        args.ApplicationMessage.Payload,
                        isCacheable: CacheTtl > TimeSpan.Zero,
                        canReuseAcrossInvokers: !isExecutorSpecific)
                    .ConfigureAwait(false);

                if (cachedResponse != null)
                {
                    Trace.TraceInformation($"Command '{_commandName}' has a cached response. Will use cached response instead of executing the command again.");

                    await GetDispatcher()(
                        async () =>
                        {
                            MqttApplicationMessage cachedMessage = await cachedResponse.ConfigureAwait(false);
                            await GenerateAndPublishResponse(commandExpirationTime, args.ApplicationMessage.ResponseTopic, args.ApplicationMessage.CorrelationData, cachedMessage.Payload, cachedMessage.UserProperties, cachedMessage.ContentType, (int)cachedMessage.PayloadFormatIndicator).ConfigureAwait(false);
                        },
                        async () => { await args.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);

                    return;
                }

                TReq request;
                CommandRequestMetadata requestMetadata;
                try
                {
                    requestMetadata = new CommandRequestMetadata(args.ApplicationMessage)
                    {
                        ContentType = args.ApplicationMessage.ContentType,
                        PayloadFormatIndicator = args.ApplicationMessage.PayloadFormatIndicator,
                    };
                    request = _serializer.FromBytes<TReq>(args.ApplicationMessage.Payload, requestMetadata.ContentType, requestMetadata.PayloadFormatIndicator);
                    // Update application HLC against received timestamp
                    if (requestMetadata.Timestamp != null)
                    {
                        await _applicationContext.ApplicationHlc.UpdateWithOtherAsync(requestMetadata.Timestamp);
                    }
                    else
                    {
                        Trace.TraceInformation($"No timestamp present in command request metadata.");
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Command '{_commandName}' invocation failed during response message contruction. Error message: {ex.Message}");
                    AkriMqttException? amex = ex as AkriMqttException;
                    CommandStatusCode statusCode = amex != null ? ErrorKindToStatusCode(amex.Kind) : CommandStatusCode.InternalServerError;

                    if (amex != null
                        && amex.Kind == AkriMqttErrorKind.HeaderInvalid
                        && amex.HeaderName != null
                        && amex.HeaderName.Equals("Content Type", StringComparison.Ordinal))
                    {
                        statusCode = CommandStatusCode.UnsupportedMediaType;
                    }

                    await GetDispatcher()(
                        async () => { await GenerateAndPublishResponseAsync(commandExpirationTime, args.ApplicationMessage.ResponseTopic, args.ApplicationMessage.CorrelationData, statusCode, ex.Message, null, null, amex?.InApplication, amex?.HeaderName, amex?.HeaderValue, requestedProtocolVersion).ConfigureAwait(false); },
                        async () => { await args.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);

                    return;
                }

                ExtendedRequest<TReq> extendedRequest = new() { Request = request, RequestMetadata = requestMetadata };

                async Task CmdFunc()
                {
                    DateTime executionStartTime = WallClock.UtcNow;
                    TimeSpan startupDelay = executionStartTime - messageReceivedTime;
                    TimeSpan remainingCommandTimeout = commandTimeout - startupDelay;
                    TimeSpan cancellationTimeout = remainingCommandTimeout < ExecutionTimeout ? remainingCommandTimeout : ExecutionTimeout;
                    using CancellationTokenSource commandCts = WallClock.CreateCancellationTokenSource(cancellationTimeout);

                    try
                    {
                        ExtendedResponse<TResp> extended = await Task.Run(() => OnCommandReceived(extendedRequest, commandCts.Token)).WaitAsync(ExecutionTimeout).ConfigureAwait(false);

                        var serializedPayloadContext = _serializer.ToBytes(extended.Response);

                        MqttApplicationMessage? responseMessage = await GenerateResponseAsync(commandExpirationTime, args.ApplicationMessage.ResponseTopic, args.ApplicationMessage.CorrelationData, !serializedPayloadContext.SerializedPayload.IsEmpty ? CommandStatusCode.OK : CommandStatusCode.NoContent, null, serializedPayloadContext, extended.ResponseMetadata);
                        await _commandResponseCache.StoreAsync(
                            _commandName,
                            sourceId,
                            args.ApplicationMessage.ResponseTopic,
                            args.ApplicationMessage.CorrelationData,
                            args.ApplicationMessage.Payload,
                            responseMessage,
                            IsIdempotent,
                            commandExpirationTime,
                            WallClock.UtcNow - executionStartTime).ConfigureAwait(false);

                        await PublishResponseAsync(args.ApplicationMessage.ResponseTopic, args.ApplicationMessage.CorrelationData, responseMessage);
                    }
                    catch (Exception ex)
                    {
                        CommandStatusCode statusCode;
                        string? statusMessage;
                        bool isAppError;
                        switch (ex)
                        {
                            case OperationCanceledException:
                            case TimeoutException:
                                statusCode = CommandStatusCode.RequestTimeout;
                                statusMessage = $"Executor timed out after {cancellationTimeout.TotalSeconds} seconds.";
                                isAppError = false;
                                invalidPropertyName = nameof(ExecutionTimeout);
                                invalidPropertyValue = XmlConvert.ToString(ExecutionTimeout);
                                Trace.TraceWarning($"Command '{_commandName}' execution timed out after {cancellationTimeout.TotalSeconds} seconds.");
                                break;
                            case InvocationException iex:
                                statusCode = CommandStatusCode.UnprocessableContent;
                                statusMessage = iex.Message;
                                isAppError = true;
                                invalidPropertyName = iex.InvalidPropertyName;
                                invalidPropertyValue = iex.InvalidPropertyValue;
                                Trace.TraceWarning($"Command '{_commandName}' execution failed due to an invocation error: {iex}.");
                                break;
                            case AkriMqttException amex:
                                statusCode = CommandStatusCode.InternalServerError;
                                statusMessage = amex.Message;
                                isAppError = true;
                                invalidPropertyName = amex?.HeaderName ?? amex?.PropertyName;
                                invalidPropertyValue = amex?.HeaderValue ?? amex?.PropertyValue?.ToString();
                                Trace.TraceWarning($"Command '{_commandName}' execution failed due to Akri Mqtt error: {amex}.");
                                break;
                            default:
                                statusCode = CommandStatusCode.InternalServerError;
                                statusMessage = ex.Message;
                                isAppError = true;
                                invalidPropertyName = null;
                                invalidPropertyValue = null;
                                Trace.TraceWarning($"Command '{_commandName}' execution failed due to error: {ex}.");
                                break;
                        }

                        await GenerateAndPublishResponseAsync(commandExpirationTime, args.ApplicationMessage.ResponseTopic, args.ApplicationMessage.CorrelationData, statusCode, statusMessage, null, null, isAppError, invalidPropertyName, invalidPropertyValue, requestedProtocolVersion);
                    }
                    finally
                    {
                        commandCts.Dispose();
                    }
                }

                await GetDispatcher()(CmdFunc, async () => { await args.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
            }
        }

        public async Task StartAsync(int? preferredDispatchConcurrency = null, IReadOnlyDictionary<string, string>? transientTopicTokenMap = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (!_isRunning)
            {
                if (_mqttClient.ProtocolVersion != MqttProtocolVersion.V500)
                {
                    throw AkriMqttException.GetConfigurationInvalidException(
                        "MQTTClient.ProtocolVersion",
                        _mqttClient.ProtocolVersion,
                        "The provided MQTT client is not configured for MQTT version 5",
                        commandName: _commandName);
                }

                string? clientId = _mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before starting a command executor");
                }

                _dispatcher ??= ExecutionDispatcher.CollectionInstance.GetDispatcher(clientId, preferredDispatchConcurrency);

                CheckProperties();

                await _commandResponseCache.StartAsync().ConfigureAwait(false);

                if (!_hasSubscribed)
                {
                    await SubscribeAsync(transientTopicTokenMap, cancellationToken).ConfigureAwait(false);
                }

                _isRunning = true;
                Trace.TraceInformation($"Command executor for '{_commandName}' started.");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (_isRunning && _hasSubscribed)
            {
                MqttClientUnsubscribeOptions mqttUnsubscribeOptions = new(_subscriptionTopic);

                MqttClientUnsubscribeResult unsubAck = await _mqttClient.UnsubscribeAsync(mqttUnsubscribeOptions, cancellationToken).ConfigureAwait(false);

                unsubAck.ThrowIfNotSuccessUnsubAck(_commandName);
                _isRunning = false;
                _hasSubscribed = false;
            }
            Trace.TraceInformation($"Command executor for '{_commandName}' stopped.");
        }

        private async Task SubscribeAsync(IReadOnlyDictionary<string, string>? transientTopicTokenMap, CancellationToken cancellationToken = default)
        {
            string requestTopicFilter = ServiceGroupId != string.Empty ? $"$share/{ServiceGroupId}/{GetCommandTopic(transientTopicTokenMap)}" : GetCommandTopic(transientTopicTokenMap);

            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce;
            MqttClientSubscribeOptions mqttSubscribeOptions = new(new MqttTopicFilter(requestTopicFilter, qos));

            MqttClientSubscribeResult subAck = await _mqttClient.SubscribeAsync(mqttSubscribeOptions, cancellationToken).ConfigureAwait(false);
            subAck.ThrowIfNotSuccessSubAck(qos, _commandName);

            _hasSubscribed = true;
            _subscriptionTopic = requestTopicFilter;
        }

        private bool TryValidateRequestHeaders(
            MqttApplicationMessage requestMsg,
            out CommandStatusCode? status,
            out string? statusMessage,
            out string? invalidPropertyName,
            out string? invalidPropertyValue)
        {
            if (requestMsg.MessageExpiryInterval == default)
            {
                status = CommandStatusCode.BadRequest;
                statusMessage = $"No message expiry interval present.";
                invalidPropertyName = "Message Expiry";
                invalidPropertyValue = null;
                return false;
            }

            if (!MqttTopicProcessor.IsValidReplacement(requestMsg.ResponseTopic))
            {
                Trace.TraceError($"Command '{this._commandName}' with CorrelationId {requestMsg.CorrelationData} specified invalid response topic '{requestMsg.ResponseTopic}'. The command response will not be published.");

                status = null;
                statusMessage = null;
                invalidPropertyName = null;
                invalidPropertyValue = null;
                return false;
            }

            if (requestMsg.CorrelationData == null || requestMsg.CorrelationData.Length == 0)
            {
                status = CommandStatusCode.BadRequest;
                statusMessage = $"No correlation data present.";
                invalidPropertyName = "Correlation Data";
                invalidPropertyValue = null;
                return false;
            }

            if (!GuidExtensions.TryParseBytes(requestMsg.CorrelationData, out Guid? correlationId))
            {
                status = CommandStatusCode.BadRequest;
                statusMessage = $"Correlation data bytes do not conform to a GUID.";
                invalidPropertyName = "Correlation Data";
                invalidPropertyValue = null;
                return false;
            }

            string? requestProtocolVersion = requestMsg.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.ProtocolVersion)?.Value;
            if (!ProtocolVersion.TryParseProtocolVersion(requestProtocolVersion, out ProtocolVersion? protocolVersion))
            {
                status = CommandStatusCode.NotSupportedVersion;
                statusMessage = $"Unparsable protocol version ({AkriSystemProperties.ProtocolVersion}) value provided: {requestProtocolVersion}.";
                invalidPropertyName = null;
                invalidPropertyValue = null;
                return false;
            }

            if (!_supportedMajorProtocolVersions.Contains(protocolVersion!.MajorVersion))
            {
                status = CommandStatusCode.NotSupportedVersion;
                statusMessage = $"Invalid or unsupported protocol version ({AkriSystemProperties.ProtocolVersion}) value provided: {requestProtocolVersion}.";
                invalidPropertyName = null;
                invalidPropertyValue = null;
                return false;
            }

            status = null;
            statusMessage = null;
            invalidPropertyName = null;
            invalidPropertyValue = null;
            return true;
        }

        private async Task<MqttApplicationMessage> GenerateResponseAsync(
            DateTime commandExpirationTime,
            string topic,
            byte[] correlationData,
            CommandStatusCode status,
            string? statusMessage = null,
            SerializedPayloadContext? payloadContext = null,
            CommandResponseMetadata? metadata = null,
            bool? isAppError = null,
            string? invalidPropertyName = null,
            string? invalidPropertyValue = null,
            string? requestedProtocolVersion = null)
        {
            MqttApplicationMessage message = new(topic, MqttQualityOfServiceLevel.AtLeastOnce)
            {
                CorrelationData = correlationData,
            };

            message.AddUserProperty(AkriSystemProperties.Status, ((int)status).ToString(CultureInfo.InvariantCulture));

            if (statusMessage != null)
            {
                message.AddUserProperty(AkriSystemProperties.StatusMessage, statusMessage);
            }

            if (payloadContext != null && !payloadContext.SerializedPayload.IsEmpty)
            {
                message.Payload = payloadContext.SerializedPayload;
                message.PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext.PayloadFormatIndicator;
                message.ContentType = payloadContext.ContentType;
            }

            message.AddUserProperty(AkriSystemProperties.ProtocolVersion, $"{CommandVersion.MajorProtocolVersion}.{CommandVersion.MinorProtocolVersion}");

            // Update HLC and use as the timestamp.
            string timestamp = await _applicationContext.ApplicationHlc.UpdateNowAsync();
            message.AddUserProperty(AkriSystemProperties.Timestamp, timestamp);

            metadata?.MarshalTo(message);

            if (isAppError != null)
            {
                message.AddUserProperty(AkriSystemProperties.IsApplicationError, (bool)isAppError ? "true" : "false");
            }

            if (invalidPropertyName != null)
            {
                message.AddUserProperty(AkriSystemProperties.InvalidPropertyName, invalidPropertyName);
            }

            if (invalidPropertyValue != null)
            {
                message.AddUserProperty(AkriSystemProperties.InvalidPropertyValue, invalidPropertyValue);
            }

            if (status == CommandStatusCode.NotSupportedVersion)
            {
                Debug.Assert(requestedProtocolVersion != null);
                message.AddUserProperty(AkriSystemProperties.RequestedProtocolVersion, requestedProtocolVersion);
                string spaceSeparatedListOfSupportedProtocolVersions = ProtocolVersion.ToString(_supportedMajorProtocolVersions);
                message.AddUserProperty(AkriSystemProperties.SupportedMajorProtocolVersions, spaceSeparatedListOfSupportedProtocolVersions);
            }

            int remainingSeconds = Math.Max(0, (int)(commandExpirationTime - WallClock.UtcNow).TotalSeconds);

            message.MessageExpiryInterval = (uint)remainingSeconds;

            return message;
        }

        private async Task GenerateAndPublishResponseAsync(
            DateTime commandExpirationTime,
            string topic,
            byte[] correlationData,
            CommandStatusCode status,
            string? statusMessage = null,
            SerializedPayloadContext? payloadContext = null,
            CommandResponseMetadata? metadata = null,
            bool? isAppError = null,
            string? invalidPropertyName = null,
            string? invalidPropertyValue = null,
            string? requestedProtocolVersion = null)
        {
            MqttApplicationMessage responseMessage = await GenerateResponseAsync(commandExpirationTime, topic, correlationData, status, statusMessage, payloadContext, metadata, isAppError, invalidPropertyName, invalidPropertyValue, requestedProtocolVersion);
            await PublishResponseAsync(topic, correlationData, responseMessage);
        }

        private Task GenerateAndPublishResponse(
            DateTime commandExpirationTime,
            string topic,
            byte[]? correlationData,
            ReadOnlySequence<byte> payload,
            List<MqttUserProperty>? userProperties,
            string? contentType,
            int payloadFormatIndicator)
        {
            MqttApplicationMessage message = new(topic, MqttQualityOfServiceLevel.AtLeastOnce)
            {
                CorrelationData = correlationData,
            };

            if (!payload.IsEmpty)
            {
                message.Payload = payload;
                message.PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadFormatIndicator;
                message.ContentType = contentType;
            }

            if (userProperties != null)
            {
                foreach (MqttUserProperty property in userProperties)
                {
                    message.AddUserProperty(property.Name, property.Value);
                }
            }

            message.AddUserProperty(AkriSystemProperties.ProtocolVersion, CommandVersion.MajorProtocolVersion + "." + CommandVersion.MinorProtocolVersion);

            int remainingSeconds = Math.Max(0, (int)(commandExpirationTime - WallClock.UtcNow).TotalSeconds);

            message.MessageExpiryInterval = (uint)remainingSeconds;

            return PublishResponseAsync(topic, correlationData, message);
        }

        private async Task PublishResponseAsync(
            string topic,
            byte[]? correlationData,
            MqttApplicationMessage responseMessage)
        {
            if (responseMessage.MessageExpiryInterval == 0)
            {
                string correlationId = correlationData != null ? $"'{new Guid(correlationData)}'" : "unknown";
                Trace.TraceError($"Command '{_commandName}' with CorrelationId {correlationId} took too long to process on topic '{topic}'. The command response will not be published.");
                return;
            }

            try
            {
                MqttClientPublishResult pubAck = await _mqttClient.PublishAsync(responseMessage, CancellationToken.None).ConfigureAwait(false);
                MqttClientPublishReasonCode pubReasonCode = pubAck.ReasonCode;
                if (pubReasonCode != MqttClientPublishReasonCode.Success)
                {
                    string correlationId = correlationData != null ? $"'{new Guid(correlationData)}'" : "unknown";
                    Trace.TraceError($"The response to command {_commandName} with CorrelationId {correlationId} failed on topic '{topic}' with publishing reason code '{pubReasonCode}'");
                }
            }
            catch (Exception e)
            {
                Trace.TraceError($"Command '{_commandName}' execution failed due to a MQTT communication error: {e.Message}.");
            }
        }

        private Dispatcher GetDispatcher()
        {
            if (_dispatcher == null)
            {
                string? clientId = _mqttClient.ClientId;
                Debug.Assert(!string.IsNullOrEmpty(clientId));
                _dispatcher = ExecutionDispatcher.CollectionInstance.GetDispatcher(clientId);
            }

            return _dispatcher;
        }

        private string GetCommandTopic(IReadOnlyDictionary<string, string>? transientTopicTokenMap)
        {
            StringBuilder commandTopic = new();

            if (TopicNamespace != null)
            {
                commandTopic.Append(TopicNamespace);
                commandTopic.Append('/');
            }

            commandTopic.Append(MqttTopicProcessor.ResolveTopic(RequestTopicPattern, EffectiveTopicTokenMap, transientTopicTokenMap));

            return commandTopic.ToString();
        }

        private void CheckProperties()
        {
            if (TopicNamespace != null && !MqttTopicProcessor.IsValidReplacement(TopicNamespace))
            {
                throw AkriMqttException.GetConfigurationInvalidException(nameof(TopicNamespace), TopicNamespace, "MQTT topic namespace is not valid", commandName: _commandName);
            }

            PatternValidity patternValidity = MqttTopicProcessor.ValidateTopicPattern(RequestTopicPattern, EffectiveTopicTokenMap, null, requireReplacement: false, out string errMsg, out string? errToken, out string? errReplacement);
            if (patternValidity != PatternValidity.Valid)
            {
                throw patternValidity switch
                {
                    PatternValidity.InvalidResidentReplacement => AkriMqttException.GetConfigurationInvalidException(errToken!, errReplacement!, errMsg, commandName: _commandName),
                    _ => AkriMqttException.GetConfigurationInvalidException(nameof(RequestTopicPattern), RequestTopicPattern, errMsg, commandName: _commandName),
                };
            }

            if (CacheTtl < TimeSpan.Zero)
            {
                throw AkriMqttException.GetConfigurationInvalidException("CacheTtl", CacheTtl, "CacheTtl must not have a negative value", commandName: _commandName);
            }

            if (!IsIdempotent && CacheTtl != TimeSpan.Zero)
            {
                throw AkriMqttException.GetConfigurationInvalidException("CacheTtl", CacheTtl, "CacheTtl must be zero when IsIdempotent=false", commandName: _commandName);
            }

            if (ExecutionTimeout <= TimeSpan.Zero)
            {
                throw AkriMqttException.GetConfigurationInvalidException("ExecutionTimeout", ExecutionTimeout, "ExecutionTimeout must have a positive value", commandName: _commandName);
            }
        }

        private static CommandStatusCode ErrorKindToStatusCode(AkriMqttErrorKind errorKind)
        {
            return errorKind switch
            {
                AkriMqttErrorKind.HeaderMissing => CommandStatusCode.BadRequest,
                AkriMqttErrorKind.HeaderInvalid => CommandStatusCode.BadRequest,
                AkriMqttErrorKind.PayloadInvalid => CommandStatusCode.BadRequest,
                AkriMqttErrorKind.StateInvalid => CommandStatusCode.ServiceUnavailable,
                AkriMqttErrorKind.InternalLogicError => CommandStatusCode.InternalServerError,
                AkriMqttErrorKind.Timeout => CommandStatusCode.RequestTimeout,
                AkriMqttErrorKind.InvocationException => CommandStatusCode.UnprocessableContent,
                AkriMqttErrorKind.ExecutionException => CommandStatusCode.InternalServerError,
                AkriMqttErrorKind.UnknownError => CommandStatusCode.InternalServerError,
                _ => CommandStatusCode.InternalServerError,
            };
        }

        public virtual async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore(false);
            GC.SuppressFinalize(this);
        }

        public virtual async ValueTask DisposeAsync(bool disposing)
        {
            await DisposeAsyncCore(disposing);
        }

        protected virtual async ValueTask DisposeAsyncCore(bool disposing)
        {
            if (!_isDisposed)
            {
                try
                {
                    await StopAsync();
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("Failed to stop the command executor while disposing it: {0}", ex);
                }

                TopicTokenMap?.Clear();
                _mqttClient.ApplicationMessageReceivedAsync -= MessageReceivedCallbackAsync;

                if (disposing)
                {
                    await _mqttClient.DisposeAsync(disposing);
                }

                _isDisposed = true;
            }
        }
    }
}
