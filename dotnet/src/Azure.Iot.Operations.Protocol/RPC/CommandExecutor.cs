// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using System;
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
        private const int majorProtocolVersion = 1;
        private const int minorProtocolVersion = 0;

        private readonly int[] supportedMajorProtocolVersions = [1];

        private static readonly TimeSpan DefaultExecutorTimeout = TimeSpan.FromSeconds(10);

        internal static IWallClock WallClock = new WallClock();

        private readonly IMqttPubSubClient mqttClient;
        private readonly string commandName;
        private readonly IPayloadSerializer serializer;

        private readonly Dictionary<string, string> topicTokenMap = [];

        private readonly HybridLogicalClock hybridLogicalClock;
        private readonly ICommandResponseCache commandResponseCache;
        private Dispatcher? dispatcher;
        private bool isRunning;
        private bool hasSubscribed;
        private string subscriptionTopic;

        private bool isDisposed;

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
        public virtual Dictionary<string, string> TopicTokenMap => topicTokenMap;

        /// <summary>
        /// Gets a dictionary used by this class's code for substituting tokens in request and response topic patterns.
        /// Can be overridden by a derived class, enabling the key/value pairs to be augmented and/or combined with other key/value pairs.
        /// </summary>
        protected virtual IReadOnlyDictionary<string, string> EffectiveTopicTokenMap => topicTokenMap;

        public CommandExecutor(IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer serializer)
        {
            if (commandName == null || commandName == string.Empty)
            {
                throw AkriMqttException.GetConfigurationInvalidException(nameof(commandName), string.Empty);
            }

            this.mqttClient = mqttClient ?? throw AkriMqttException.GetArgumentInvalidException(commandName, nameof(mqttClient), string.Empty);
            this.commandName = commandName;
            this.serializer = serializer ?? throw AkriMqttException.GetArgumentInvalidException(commandName, nameof(serializer), string.Empty);

            isRunning = false;
            hasSubscribed = false;
            subscriptionTopic = string.Empty;

            ExecutionTimeout = DefaultExecutorTimeout;

            hybridLogicalClock = HybridLogicalClock.GetInstance();

            commandResponseCache = CommandResponseCache.GetCache();

            dispatcher = null;

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
                    await GetDispatcher()(
                        status != null ? async () => { await GenerateAndPublishResponse(commandExpirationTime, args.ApplicationMessage.ResponseTopic!, args.ApplicationMessage.CorrelationData!, (CommandStatusCode)status, statusMessage, null, null, false, invalidPropertyName, invalidPropertyValue, requestedProtocolVersion).ConfigureAwait(false); }
                    : null,
                        async () => { await args.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
                    return;
                }

                // This validation is handled above, so assume a response topic is provided beyond this point.
                Debug.Assert(args.ApplicationMessage.ResponseTopic != null);
                Debug.Assert(args.ApplicationMessage.CorrelationData != null);

                string? clientId = this.mqttClient.ClientId;
                Debug.Assert(!string.IsNullOrEmpty(clientId));
                string executorId = ExecutorId ?? clientId;
                bool isExecutorSpecific = args.ApplicationMessage.Topic.Contains(executorId);
                string sourceId = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.SourceId)?.Value ?? string.Empty;

                Task<MqttApplicationMessage>? cachedResponse =
                    await commandResponseCache.RetrieveAsync(
                        this.commandName,
                        sourceId,
                        args.ApplicationMessage.Topic,
                        args.ApplicationMessage.CorrelationData,
                        args.ApplicationMessage.PayloadSegment.Array ?? [],
                        isCacheable: CacheTtl > TimeSpan.Zero,
                        canReuseAcrossInvokers: !isExecutorSpecific)
                    .ConfigureAwait(false);

                if (cachedResponse != null)
                {
                    await GetDispatcher()(
                        async () =>
                        {
                            MqttApplicationMessage cachedMessage = await cachedResponse.ConfigureAwait(false);
                            await GenerateAndPublishResponse(commandExpirationTime, args.ApplicationMessage.ResponseTopic, args.ApplicationMessage.CorrelationData, cachedMessage.PayloadSegment, cachedMessage.UserProperties, cachedMessage.ContentType, (int)cachedMessage.PayloadFormatIndicator).ConfigureAwait(false);
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
                    request = this.serializer.FromBytes<TReq>(args.ApplicationMessage.PayloadSegment.Array, requestMetadata.ContentType, requestMetadata.PayloadFormatIndicator);
                    hybridLogicalClock.Update(requestMetadata.Timestamp);
                }
                catch (Exception ex)
                {
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
                        async () => { await GenerateAndPublishResponse(commandExpirationTime, args.ApplicationMessage.ResponseTopic, args.ApplicationMessage.CorrelationData, statusCode, ex.Message, null, null, amex?.InApplication, amex?.HeaderName, amex?.HeaderValue, requestedProtocolVersion).ConfigureAwait(false); },
                        async () => { await args.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);

                    return;
                }

                ExtendedRequest<TReq> extendedRequest = new() { Request = request, RequestMetadata = requestMetadata };

                async Task cmdFunc()
                {
                    DateTime executionStartTime = WallClock.UtcNow;
                    TimeSpan startupDelay = executionStartTime - messageReceivedTime;
                    TimeSpan remainingCommandTimeout = commandTimeout - startupDelay;
                    TimeSpan cancellationTimeout = remainingCommandTimeout < ExecutionTimeout ? remainingCommandTimeout : ExecutionTimeout;
                    using CancellationTokenSource commandCts = WallClock.CreateCancellationTokenSource(cancellationTimeout);

                    try
                    {
                        ExtendedResponse<TResp> extended = await Task.Run(() => OnCommandReceived(extendedRequest, commandCts.Token)).WaitAsync(ExecutionTimeout).ConfigureAwait(false);

                        var serializedPayloadContext = serializer.ToBytes(extended.Response);

                        MqttApplicationMessage? responseMessage = GenerateResponse(commandExpirationTime, args.ApplicationMessage.ResponseTopic, args.ApplicationMessage.CorrelationData, serializedPayloadContext.SerializedPayload != null ? CommandStatusCode.OK : CommandStatusCode.NoContent, null, serializedPayloadContext, extended.ResponseMetadata);
                        await commandResponseCache.StoreAsync(
                            this.commandName,
                            sourceId,
                            args.ApplicationMessage.Topic,
                            args.ApplicationMessage.CorrelationData,
                            args.ApplicationMessage.PayloadSegment.Array,
                            responseMessage,
                            IsIdempotent,
                            commandExpirationTime,
                            WallClock.UtcNow - executionStartTime).ConfigureAwait(false);

                        await PublishResponse(args.ApplicationMessage.ResponseTopic, args.ApplicationMessage.CorrelationData, responseMessage);
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
                                Trace.TraceWarning($"Command '{this.commandName}' execution timed out after {cancellationTimeout.TotalSeconds} seconds.");
                                break;
                            case InvocationException iex:
                                statusCode = CommandStatusCode.UnprocessableContent;
                                statusMessage = iex.Message;
                                isAppError = true;
                                invalidPropertyName = iex.InvalidPropertyName;
                                invalidPropertyValue = iex.InvalidPropertyValue;
                                Trace.TraceWarning($"Command '{this.commandName}' execution failed due to an invocation error: {iex}.");
                                break;
                            case AkriMqttException amex:
                                statusCode = CommandStatusCode.InternalServerError;
                                statusMessage = amex.Message;
                                isAppError = true;
                                invalidPropertyName = amex?.HeaderName ?? amex?.PropertyName;
                                invalidPropertyValue = amex?.HeaderValue ?? amex?.PropertyValue?.ToString();
                                Trace.TraceWarning($"Command '{this.commandName}' execution failed due to Akri Mqtt error: {amex}.");
                                break;
                            default:
                                statusCode = CommandStatusCode.InternalServerError;
                                statusMessage = ex.Message;
                                isAppError = true;
                                invalidPropertyName = null;
                                invalidPropertyValue = null;
                                Trace.TraceWarning($"Command '{this.commandName}' execution failed due to error: {ex}.");
                                break;
                        }

                        await GenerateAndPublishResponse(commandExpirationTime, args.ApplicationMessage.ResponseTopic, args.ApplicationMessage.CorrelationData, statusCode, statusMessage, null, null, isAppError, invalidPropertyName, invalidPropertyValue, requestedProtocolVersion);
                    }
                    finally
                    {
                        commandCts.Dispose();
                    }
                }

                await GetDispatcher()(cmdFunc, async () => { await args.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
            }
        }

        public async Task StartAsync(int? preferredDispatchConcurrency = null, IReadOnlyDictionary<string, string>? transientTopicTokenMap = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(isDisposed, this);

            if (!isRunning)
            {
                if (mqttClient.ProtocolVersion != MqttProtocolVersion.V500)
                {
                    throw AkriMqttException.GetConfigurationInvalidException(
                        "MQTTClient.ProtocolVersion",
                        mqttClient.ProtocolVersion,
                        "The provided MQTT client is not configured for MQTT version 5",
                        commandName: commandName);
                }

                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before starting a command executor");
                }

                dispatcher ??= ExecutionDispatcher.CollectionInstance.GetDispatcher(clientId, preferredDispatchConcurrency);

                CheckProperties();

                await commandResponseCache.StartAsync().ConfigureAwait(false);

                if (!hasSubscribed)
                {
                    await SubscribeAsync(transientTopicTokenMap, cancellationToken).ConfigureAwait(false);
                }

                isRunning = true;
                Trace.TraceInformation($"Command executor for '{commandName}' started.");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(isDisposed, this);

            if (isRunning && hasSubscribed)
            {
                MqttClientUnsubscribeOptions mqttUnsubscribeOptions = new(subscriptionTopic);

                MqttClientUnsubscribeResult unsubAck = await mqttClient.UnsubscribeAsync(mqttUnsubscribeOptions, cancellationToken).ConfigureAwait(false);

                unsubAck.ThrowIfNotSuccessUnsubAck(this.commandName);
                isRunning = false;
                hasSubscribed = false;
            }
            Trace.TraceInformation($"Command executor for '{commandName}' stopped.");
        }

        private async Task SubscribeAsync(IReadOnlyDictionary<string, string>? transientTopicTokenMap, CancellationToken cancellationToken = default)
        {
            string requestTopicFilter = ServiceGroupId != string.Empty ? $"$share/{ServiceGroupId}/{GetCommandTopic(transientTopicTokenMap)}" : GetCommandTopic(transientTopicTokenMap);

            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce;
            MqttClientSubscribeOptions mqttSubscribeOptions = new(new MqttTopicFilter(requestTopicFilter, qos));

            MqttClientSubscribeResult subAck = await mqttClient.SubscribeAsync(mqttSubscribeOptions, cancellationToken).ConfigureAwait(false);
            subAck.ThrowIfNotSuccessSubAck(qos, this.commandName);

            hasSubscribed = true;
            subscriptionTopic = requestTopicFilter;
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
                Trace.TraceError($"Command '{this.commandName}' with CorrelationId {requestMsg.CorrelationData} specified invalid response topic '{requestMsg.ResponseTopic}'. The command response will not be published.");

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

            if (!supportedMajorProtocolVersions.Contains(protocolVersion!.MajorVersion))
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

        private MqttApplicationMessage GenerateResponse(
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

            if (payloadContext != null && payloadContext.SerializedPayload != null && payloadContext.SerializedPayload.Length > 0)
            {
                message.PayloadSegment = payloadContext.SerializedPayload;
                message.PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext.PayloadFormatIndicator;
                message.ContentType = payloadContext.ContentType;
            }

            message.AddUserProperty(AkriSystemProperties.ProtocolVersion, $"{majorProtocolVersion}.{minorProtocolVersion}");

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
                string spaceSeperatedListOfSupportedProtocolVersions = ProtocolVersion.ToString(supportedMajorProtocolVersions);
                message.AddUserProperty(AkriSystemProperties.SupportedMajorProtocolVersions, spaceSeperatedListOfSupportedProtocolVersions);
            }

            int remainingSeconds = Math.Max(0, (int)(commandExpirationTime - WallClock.UtcNow).TotalSeconds);

            message.MessageExpiryInterval = (uint)remainingSeconds;

            return message;
        }

        private Task GenerateAndPublishResponse(
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
            MqttApplicationMessage responseMessage = GenerateResponse(commandExpirationTime, topic, correlationData, status, statusMessage, payloadContext, metadata, isAppError, invalidPropertyName, invalidPropertyValue, requestedProtocolVersion);
            return PublishResponse(topic, correlationData, responseMessage);
        }

        private Task GenerateAndPublishResponse(
            DateTime commandExpirationTime,
            string topic,
            byte[]? correlationData,
            ArraySegment<byte> payloadSegment,
            List<MqttUserProperty>? userProperties,
            string? contentType,
            int payloadFormatIndicator)
        {
            MqttApplicationMessage message = new(topic, MqttQualityOfServiceLevel.AtLeastOnce)
            {
                CorrelationData = correlationData,
            };

            if (payloadSegment.Count > 0)
            {
                message.PayloadSegment = payloadSegment;
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

            message.AddUserProperty(AkriSystemProperties.ProtocolVersion, majorProtocolVersion + "." + minorProtocolVersion);

            int remainingSeconds = Math.Max(0, (int)(commandExpirationTime - WallClock.UtcNow).TotalSeconds);

            message.MessageExpiryInterval = (uint)remainingSeconds;

            return PublishResponse(topic, correlationData, message);
        }

        private async Task PublishResponse(
            string topic,
            byte[]? correlationData,
            MqttApplicationMessage responseMessage)
        {
            if (responseMessage.MessageExpiryInterval == 0)
            {
                string correlationId = correlationData != null ? $"'{new Guid(correlationData)}'" : "unknown";
                Trace.TraceError($"Command '{this.commandName}' with CorrelationId {correlationId} took too long to process on topic '{topic}'. The command response will not be published.");
                return;
            }

            try
            {
                MqttClientPublishResult pubAck = await mqttClient.PublishAsync(responseMessage, CancellationToken.None).ConfigureAwait(false);
                MqttClientPublishReasonCode pubReasonCode = pubAck.ReasonCode;
                if (pubReasonCode != MqttClientPublishReasonCode.Success)
                {
                    string correlationId = correlationData != null ? $"'{new Guid(correlationData)}'" : "unknown";
                    Trace.TraceError($"The response to command {commandName} with CorrelationId {correlationId} failed on topic '{topic}' with publishing reason code '{pubReasonCode}'");
                }
            }
            catch (Exception e)
            {
                Trace.TraceError($"Command '{this.commandName}' execution failed due to a MQTT communication error: {e.Message}.");
            }
        }

        private Dispatcher GetDispatcher()
        {
            if (dispatcher == null)
            {
                string? clientId = this.mqttClient.ClientId;
                Debug.Assert(!string.IsNullOrEmpty(clientId));
                dispatcher = ExecutionDispatcher.CollectionInstance.GetDispatcher(clientId);
            }

            return dispatcher;
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
                throw AkriMqttException.GetConfigurationInvalidException(nameof(TopicNamespace), TopicNamespace, "MQTT topic namespace is not valid", commandName: commandName);
            }

            PatternValidity patternValidity = MqttTopicProcessor.ValidateTopicPattern(RequestTopicPattern, EffectiveTopicTokenMap, null, requireReplacement: false, out string errMsg, out string? errToken, out string? errReplacement);
            if (patternValidity != PatternValidity.Valid)
            {
                throw patternValidity switch
                {
                    PatternValidity.InvalidResidentReplacement => AkriMqttException.GetConfigurationInvalidException(errToken!, errReplacement!, errMsg, commandName: commandName),
                    _ => AkriMqttException.GetConfigurationInvalidException(nameof(RequestTopicPattern), RequestTopicPattern, errMsg, commandName: commandName),
                };
            }

            if (CacheTtl < TimeSpan.Zero)
            {
                throw AkriMqttException.GetConfigurationInvalidException("CacheTtl", CacheTtl, "CacheTtl must not have a negative value", commandName: commandName);
            }

            if (!IsIdempotent && CacheTtl != TimeSpan.Zero)
            {
                throw AkriMqttException.GetConfigurationInvalidException("CacheTtl", CacheTtl, "CacheTtl must be zero when IsIdempotent=false", commandName: commandName);
            }

            if (ExecutionTimeout <= TimeSpan.Zero)
            {
                throw AkriMqttException.GetConfigurationInvalidException("ExecutionTimeout", ExecutionTimeout, "ExecutionTimeout must have a positive value", commandName: commandName);
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
            if (!isDisposed)
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
                mqttClient.ApplicationMessageReceivedAsync -= MessageReceivedCallbackAsync;

                if (disposing)
                {
                    await mqttClient.DisposeAsync(disposing);
                }

                isDisposed = true;
            }
        }
    }
}