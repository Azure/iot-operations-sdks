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
    public abstract class CommandInvoker<TReq, TResp> : IAsyncDisposable
        where TReq : class
        where TResp : class
    {
        private readonly int[] _supportedMajorProtocolVersions = [CommandVersion.MajorProtocolVersion];

        private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MinimumCommandTimeout = TimeSpan.FromSeconds(1);

        internal static IWallClock WallClock = new WallClock();

        private readonly IMqttPubSubClient _mqttClient;
        private readonly string _commandName;
        private readonly IPayloadSerializer _serializer;

        private readonly Dictionary<string, string> _topicTokenMap = [];

        private readonly object _subscribedTopicsSetLock = new();
        private readonly HashSet<string> _subscribedTopics;

        private readonly object _requestIdMapLock = new();
        private readonly Dictionary<string, ResponsePromise> _requestIdMap;

        private bool _isDisposed;

        public string RequestTopicPattern { get; init; }

        public string? TopicNamespace { get; set; }

        /// <summary>
        /// The prefix to use in the command response topic. This value is ignored if <see cref="ResponseTopicPattern"/> is set.
        /// </summary>
        /// <remarks>
        /// If no prefix or suffix is specified, and no value is provided in <see cref="ResponseTopicPattern"/>, then this
        /// value will default to "clients/{invokerClientId}" for security purposes.
        /// 
        /// If a prefix and/or suffix are provided, then the response topic will use the format:
        /// {prefix}/{command request topic}/{suffix}.
        /// </remarks>
        public string? ResponseTopicPrefix { get; set; }

        /// <summary>
        /// The suffix to use in the command response topic. This value is ignored if <see cref="ResponseTopicPattern"/> is set.
        /// </summary>
        /// <remarks>
        /// If no suffix is specified, then the command response topic won't include a suffix.
        /// 
        /// If a prefix and/or suffix are provided, then the response topic will use the format:
        /// {prefix}/{command request topic}/{suffix}.
        /// </remarks>
        public string? ResponseTopicSuffix { get; set; }

        /// <summary>
        /// If provided, this topic pattern will be used for command response topic.
        /// </summary>
        /// <remarks>
        /// If not provided, and no value is provided for <see cref="ResponseTopicPrefix"/> or <see cref="ResponseTopicSuffix"/>, the default pattern used will be clients/{mqtt client id}/{request topic pattern}.
        /// </remarks>
        public string? ResponseTopicPattern { get; set; }

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

        private readonly ApplicationContext _applicationContext;
        public CommandInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer serializer)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _applicationContext = applicationContext;
            if (commandName == null || commandName == string.Empty)
            {
                throw AkriMqttException.GetConfigurationInvalidException(nameof(commandName), string.Empty);
            }

            _mqttClient = mqttClient ?? throw AkriMqttException.GetArgumentInvalidException(commandName, nameof(mqttClient), string.Empty);
            _commandName = commandName;
            _serializer = serializer ?? throw AkriMqttException.GetArgumentInvalidException(commandName, nameof(serializer), string.Empty);

            _subscribedTopics = [];
            _requestIdMap = [];

            RequestTopicPattern = AttributeRetriever.GetAttribute<CommandTopicAttribute>(this)?.RequestTopic ?? string.Empty;

            _mqttClient.ApplicationMessageReceivedAsync += MessageReceivedCallbackAsync;
        }

        private string GenerateResponseTopicPattern(IReadOnlyDictionary<string, string>? transientTopicTokenMap)
        {
            if (ResponseTopicPattern != null)
            {
                return ResponseTopicPattern;
            }

            StringBuilder responseTopicPattern = new();

            // ADR 14 specifies that a default response topic prefix should be used if
            // the user doesn't provide any prefix, suffix, or specify the response topic
            if (string.IsNullOrWhiteSpace(ResponseTopicPrefix)
                && string.IsNullOrWhiteSpace(ResponseTopicSuffix))
            {
                ResponseTopicPrefix = "clients/" + _mqttClient.ClientId;
            }

            if (ResponseTopicPrefix != null)
            {
                PatternValidity patternValidity = MqttTopicProcessor.ValidateTopicPattern(ResponseTopicPrefix, EffectiveTopicTokenMap, transientTopicTokenMap, requireReplacement: true, out string errMsg, out string? errToken, out string? errReplacement);
                if (patternValidity != PatternValidity.Valid)
                {
                    throw patternValidity switch
                    {
                        PatternValidity.MissingReplacement => AkriMqttException.GetArgumentInvalidException(_commandName, errToken!, null, errMsg),
                        PatternValidity.InvalidTransientReplacement => AkriMqttException.GetArgumentInvalidException(_commandName, errToken!, errReplacement, errMsg),
                        PatternValidity.InvalidResidentReplacement => AkriMqttException.GetConfigurationInvalidException(errToken!, errReplacement!, errMsg, commandName: _commandName),
                        _ => AkriMqttException.GetConfigurationInvalidException(nameof(ResponseTopicPrefix), ResponseTopicPrefix, errMsg, commandName: _commandName),
                    };
                }

                responseTopicPattern.Append(ResponseTopicPrefix);
                responseTopicPattern.Append('/');
            }

            responseTopicPattern.Append(RequestTopicPattern);

            if (ResponseTopicSuffix != null)
            {
                PatternValidity patternValidity = MqttTopicProcessor.ValidateTopicPattern(ResponseTopicSuffix, EffectiveTopicTokenMap, transientTopicTokenMap, requireReplacement: true, out string errMsg, out string? errToken, out string? errReplacement);
                if (patternValidity != PatternValidity.Valid)
                {
                    throw patternValidity switch
                    {
                        PatternValidity.MissingReplacement => AkriMqttException.GetArgumentInvalidException(_commandName, errToken!, null, errMsg),
                        PatternValidity.InvalidTransientReplacement => AkriMqttException.GetArgumentInvalidException(_commandName, errToken!, errReplacement, errMsg),
                        PatternValidity.InvalidResidentReplacement => AkriMqttException.GetConfigurationInvalidException(errToken!, errReplacement!, errMsg, commandName: _commandName),
                        _ => AkriMqttException.GetConfigurationInvalidException(nameof(ResponseTopicSuffix), ResponseTopicSuffix, errMsg, commandName: _commandName),
                    };
                }

                responseTopicPattern.Append('/');
                responseTopicPattern.Append(ResponseTopicSuffix);
            }

            return responseTopicPattern.ToString();
        }

        private string GetCommandTopic(string pattern, IReadOnlyDictionary<string, string>? transientTopicTokenMap)
        {
            StringBuilder commandTopic = new();

            if (TopicNamespace != null)
            {
                if (!MqttTopicProcessor.IsValidReplacement(TopicNamespace))
                {
                    throw AkriMqttException.GetConfigurationInvalidException(nameof(TopicNamespace), TopicNamespace, "MQTT topic namespace is not valid", commandName: _commandName);
                }

                commandTopic.Append(TopicNamespace);
                commandTopic.Append('/');
            }

            commandTopic.Append(MqttTopicProcessor.ResolveTopic(pattern, EffectiveTopicTokenMap, transientTopicTokenMap));

            return commandTopic.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="responseTopicFilter"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="AkriMqttException"></exception>
        internal async Task SubscribeAsNeededAsync(string responseTopicFilter, CancellationToken cancellationToken = default)
        {
            lock (_subscribedTopicsSetLock)
            {
                if (_subscribedTopics.Contains(responseTopicFilter))
                {
                    return;
                }
            }

            if (_mqttClient.ProtocolVersion != MqttProtocolVersion.V500)
            {
                throw AkriMqttException.GetConfigurationInvalidException(
                    "MQTTClient.ProtocolVersion",
                    _mqttClient.ProtocolVersion,
                    "The provided MQTT client is not configured for MQTT version 5",
                    commandName: _commandName);
            }

            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce;
            MqttClientSubscribeOptions mqttSubscribeOptions = new(responseTopicFilter, qos);

            MqttClientSubscribeResult subAck = await _mqttClient.SubscribeAsync(mqttSubscribeOptions, cancellationToken).ConfigureAwait(false);
            subAck.ThrowIfNotSuccessSubAck(qos, _commandName);

            lock (_subscribedTopicsSetLock)
            {
                _subscribedTopics.Add(responseTopicFilter);
            }
            Trace.TraceInformation($"Subscribed to topic filter '{responseTopicFilter}' for command invoker '{_commandName}'");
        }

        private async Task MessageReceivedCallbackAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            if (args.ApplicationMessage.CorrelationData != null && GuidExtensions.TryParseBytes(args.ApplicationMessage.CorrelationData, out Guid? requestGuid))
            {
                string requestGuidString = requestGuid!.Value.ToString();
                ResponsePromise? responsePromise;
                lock (_requestIdMapLock)
                {
                    if (!_requestIdMap.TryGetValue(requestGuidString, out responsePromise))
                    {
                        return;
                    }
                }

                args.AutoAcknowledge = true;
                if (MqttTopicProcessor.DoesTopicMatchFilter(args.ApplicationMessage.Topic, responsePromise.ResponseTopic))
                {
                    // Assume a protocol version of 1.0 if no protocol version was specified
                    string? responseProtocolVersion = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.ProtocolVersion)?.Value;
                    if (!ProtocolVersion.TryParseProtocolVersion(responseProtocolVersion, out ProtocolVersion? protocolVersion))
                    {
                        AkriMqttException akriException = new($"Received a response with an unparsable protocol version number: {responseProtocolVersion}")
                        {
                            Kind = AkriMqttErrorKind.UnsupportedResponseVersion,
                            InApplication = false,
                            IsShallow = false,
                            IsRemote = false,
                            CommandName = _commandName,
                            CorrelationId = requestGuid,
                            SupportedMajorProtocolVersions = _supportedMajorProtocolVersions,
                            ProtocolVersion = responseProtocolVersion,
                        };

                        SetExceptionSafe(responsePromise.CompletionSource, akriException);
                        return;
                    }

                    if (!_supportedMajorProtocolVersions.Contains(protocolVersion!.MajorVersion))
                    {
                        AkriMqttException akriException = new($"Received a response with an unsupported protocol version number: {responseProtocolVersion}")
                        {
                            Kind = AkriMqttErrorKind.UnsupportedResponseVersion,
                            InApplication = false,
                            IsShallow = false,
                            IsRemote = false,
                            CommandName = _commandName,
                            CorrelationId = requestGuid,
                            SupportedMajorProtocolVersions = _supportedMajorProtocolVersions,
                            ProtocolVersion = responseProtocolVersion,
                        };

                        SetExceptionSafe(responsePromise.CompletionSource, akriException);
                        return;
                    }

                    MqttUserProperty? statusProperty = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.Status);

                    if (!TryValidateResponseHeaders(statusProperty, requestGuidString, out AkriMqttErrorKind errorKind, out string message, out string? headerName, out string? headerValue))
                    {
                        AkriMqttException akriException = new(message)
                        {
                            Kind = errorKind,
                            InApplication = false,
                            IsShallow = false,
                            IsRemote = false,
                            HeaderName = headerName,
                            HeaderValue = headerValue,
                            CommandName = _commandName,
                            CorrelationId = requestGuid,
                        };

                        SetExceptionSafe(responsePromise.CompletionSource, akriException);
                        return;
                    }

                    int statusCode = int.Parse(statusProperty!.Value, CultureInfo.InvariantCulture);

                    if (statusCode is not ((int)CommandStatusCode.OK) and not ((int)CommandStatusCode.NoContent))
                    {
                        MqttUserProperty? invalidNameProperty = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.InvalidPropertyName);
                        MqttUserProperty? invalidValueProperty = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.InvalidPropertyValue);
                        bool isApplicationError = (args.ApplicationMessage.UserProperties?.TryGetProperty(AkriSystemProperties.IsApplicationError, out string? isAppError) ?? false) && isAppError?.ToLower(CultureInfo.InvariantCulture) != "false";
                        string? statusMessage = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.StatusMessage)?.Value;

                        errorKind = StatusCodeToErrorKind((CommandStatusCode)statusCode, isApplicationError, invalidNameProperty != null, invalidValueProperty != null);
                        AkriMqttException akriException = new(statusMessage ?? "Error condition identified by remote service")
                        {
                            Kind = errorKind,
                            InApplication = isApplicationError,
                            IsShallow = false,
                            IsRemote = true,
                            HttpStatusCode = statusCode,
                            HeaderName = UseHeaderFields(errorKind) ? invalidNameProperty?.Value : null,
                            HeaderValue = UseHeaderFields(errorKind) ? invalidValueProperty?.Value : null,
                            PropertyName = UsePropertyFields(errorKind) ? invalidNameProperty?.Value : null,
                            PropertyValue = UsePropertyFields(errorKind) ? invalidValueProperty?.Value : null,
                            TimeoutName = UseTimeoutFields(errorKind) ? invalidNameProperty?.Value : null,
                            TimeoutValue = UseTimeoutFields(errorKind) ? GetAsTimeSpan(invalidValueProperty?.Value) : null,
                            CommandName = _commandName,
                            CorrelationId = requestGuid,
                        };

                        if (errorKind == AkriMqttErrorKind.UnsupportedRequestVersion)
                        {
                            MqttUserProperty? supportedMajorVersions = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.SupportedMajorProtocolVersions);
                            MqttUserProperty? requestProtocolVersion = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.RequestedProtocolVersion);

                            if (requestProtocolVersion != null)
                            {
                                akriException.ProtocolVersion = requestProtocolVersion.Value;
                            }
                            else
                            {
                                Trace.TraceWarning("Command executor failed to provide the request's protocol version");
                            }

                            if (supportedMajorVersions != null
                                && ProtocolVersion.TryParseFromString(supportedMajorVersions!.Value, out int[]? versions))
                            {
                                akriException.SupportedMajorProtocolVersions = versions;
                            }
                            else
                            {
                                Trace.TraceWarning("Command executor failed to provide the supported major protocol versions");
                            }
                        }

                        SetExceptionSafe(responsePromise.CompletionSource, akriException);
                        return;
                    }

                    TResp response;
                    CommandResponseMetadata responseMetadata;
                    try
                    {
                        response = _serializer.FromBytes<TResp>(args.ApplicationMessage.Payload, args.ApplicationMessage.ContentType, args.ApplicationMessage.PayloadFormatIndicator);
                        responseMetadata = new CommandResponseMetadata(args.ApplicationMessage);
                    }
                    catch (Exception ex)
                    {
                        SetExceptionSafe(responsePromise.CompletionSource, ex);
                        return;
                    }

                    if (responseMetadata.Timestamp != null)
                    {
                        await _applicationContext.ApplicationHlc.UpdateWithOtherAsync(responseMetadata.Timestamp);
                    }
                    else
                    {
                        Trace.TraceInformation($"No timestamp present in command response metadata.");
                    }

                    ExtendedResponse<TResp> extendedResponse = new() { Response = response, ResponseMetadata = responseMetadata };

                    if (!responsePromise.CompletionSource.TrySetResult(extendedResponse))
                    {
                        Trace.TraceWarning("Failed to complete the command response promise. This may be because the operation was cancelled or finished with exception.");
                    }
                }
            }

            return;
        }

        private static bool TryValidateResponseHeaders(
            MqttUserProperty? statusProperty,
            string correlationId,
            out AkriMqttErrorKind errorKind,
            out string message,
            out string? headerName,
            out string? headerValue)
        {
            if (!Guid.TryParse(correlationId, out _))
            {
                errorKind = AkriMqttErrorKind.HeaderInvalid;
                message = $"Correlation data '{correlationId}' is not a string representation of a GUID.";
                headerName = "Correlation Data";
                headerValue = correlationId;
                return false;
            }

            if (statusProperty == null)
            {
                errorKind = AkriMqttErrorKind.HeaderMissing;
                message = $"response missing MQTT user property \"{AkriSystemProperties.Status}\"";
                headerName = AkriSystemProperties.Status;
                headerValue = null;
                return false;
            }

            if (!int.TryParse(statusProperty.Value, out _))
            {
                errorKind = AkriMqttErrorKind.HeaderInvalid;
                message = $"unparseable status code in response: \"{statusProperty.Value}\"";
                headerName = AkriSystemProperties.Status;
                headerValue = statusProperty.Value;
                return false;
            }

            errorKind = AkriMqttErrorKind.UnknownError;
            message = string.Empty;
            headerName = null;
            headerValue = null;
            return true;
        }

        private static AkriMqttErrorKind StatusCodeToErrorKind(CommandStatusCode statusCode, bool isAppError, bool hasInvalidName, bool hasInvalidValue)
        {
            return statusCode switch
            {
                CommandStatusCode.BadRequest =>
                    hasInvalidValue ? AkriMqttErrorKind.HeaderInvalid :
                    hasInvalidName ? AkriMqttErrorKind.HeaderMissing :
                    AkriMqttErrorKind.PayloadInvalid,
                CommandStatusCode.RequestTimeout => AkriMqttErrorKind.Timeout,
                CommandStatusCode.UnsupportedMediaType => AkriMqttErrorKind.HeaderInvalid,
                CommandStatusCode.InternalServerError =>
                    isAppError ? AkriMqttErrorKind.ExecutionException :
                    hasInvalidName ? AkriMqttErrorKind.InternalLogicError :
                    AkriMqttErrorKind.UnknownError,
                CommandStatusCode.NotSupportedVersion => AkriMqttErrorKind.UnsupportedRequestVersion,
                CommandStatusCode.ServiceUnavailable => AkriMqttErrorKind.StateInvalid,
                _ => AkriMqttErrorKind.UnknownError,
            };
        }

        private static bool UseHeaderFields(AkriMqttErrorKind errorKind)
        {
            return errorKind is AkriMqttErrorKind.HeaderMissing or AkriMqttErrorKind.HeaderInvalid;
        }

        private static bool UseTimeoutFields(AkriMqttErrorKind errorKind)
        {
            return errorKind == AkriMqttErrorKind.Timeout;
        }

        private static bool UsePropertyFields(AkriMqttErrorKind errorKind)
        {
            return !UseHeaderFields(errorKind) && !UseTimeoutFields(errorKind);
        }

        private static TimeSpan? GetAsTimeSpan(string? value)
        {
            return value != null ? XmlConvert.ToTimeSpan(value) : null;
        }

        public async Task<ExtendedResponse<TResp>> InvokeCommandAsync(TReq request, CommandRequestMetadata? metadata = null, IReadOnlyDictionary<string, string>? transientTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            Guid requestGuid = metadata?.CorrelationId ?? Guid.NewGuid();

            TimeSpan reifiedCommandTimeout = commandTimeout ?? DefaultCommandTimeout;
            // Rounding up to the nearest second
            reifiedCommandTimeout = TimeSpan.FromSeconds(Math.Ceiling(reifiedCommandTimeout.TotalSeconds));


            if (reifiedCommandTimeout < MinimumCommandTimeout)
            {
                throw AkriMqttException.GetArgumentInvalidException("commandTimeout", nameof(commandTimeout), reifiedCommandTimeout, $"commandTimeout must be at least {MinimumCommandTimeout}");
            }

            if (reifiedCommandTimeout.TotalSeconds > uint.MaxValue)
            {
                throw AkriMqttException.GetArgumentInvalidException("commandTimeout", nameof(commandTimeout), reifiedCommandTimeout, $"commandTimeout cannot be larger than {uint.MaxValue} seconds");
            }

            if (_requestIdMap.ContainsKey(requestGuid.ToString()))
            {
                throw new AkriMqttException($"Command '{_commandName}' invocation failed due to duplicate request with same correlationId")
                {
                    Kind = AkriMqttErrorKind.StateInvalid,
                    InApplication = false,
                    IsShallow = true,
                    IsRemote = false,
                    CommandName = _commandName,
                    CorrelationId = requestGuid,
                };
            }

            PatternValidity patternValidity = MqttTopicProcessor.ValidateTopicPattern(RequestTopicPattern, EffectiveTopicTokenMap, transientTopicTokenMap, requireReplacement: true, out string errMsg, out string? errToken, out string? errReplacement);
            if (patternValidity != PatternValidity.Valid)
            {
                throw patternValidity switch
                {
                    PatternValidity.MissingReplacement => AkriMqttException.GetArgumentInvalidException(_commandName, errToken!, null, errMsg),
                    PatternValidity.InvalidTransientReplacement => AkriMqttException.GetArgumentInvalidException(_commandName, errToken!, errReplacement, errMsg),
                    PatternValidity.InvalidResidentReplacement => AkriMqttException.GetConfigurationInvalidException(errToken!, errReplacement!, errMsg, commandName: _commandName),
                    _ => AkriMqttException.GetConfigurationInvalidException(nameof(RequestTopicPattern), RequestTopicPattern, errMsg, commandName: _commandName),
                };
            }

            try
            {
                string requestTopic = GetCommandTopic(RequestTopicPattern, transientTopicTokenMap);
                string responseTopicPattern = GenerateResponseTopicPattern(transientTopicTokenMap);
                string responseTopic = GetCommandTopic(responseTopicPattern, transientTopicTokenMap);
                string responseTopicFilter = GetCommandTopic(responseTopicPattern, null);

                ResponsePromise responsePromise = new(responseTopic);

                lock (_requestIdMapLock)
                {
                    _requestIdMap[requestGuid.ToString()] = responsePromise;
                }

                MqttApplicationMessage requestMessage = new(requestTopic, MqttQualityOfServiceLevel.AtLeastOnce)
                {
                    ResponseTopic = responseTopic,
                    CorrelationData = requestGuid.ToByteArray(),
                    MessageExpiryInterval = (uint)reifiedCommandTimeout.TotalSeconds,
                };

                string? clientId = _mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking a command");
                }

                requestMessage.AddUserProperty(AkriSystemProperties.ProtocolVersion, $"{CommandVersion.MajorProtocolVersion}.{CommandVersion.MinorProtocolVersion}");
                requestMessage.AddUserProperty("$partition", clientId);
                requestMessage.AddUserProperty(AkriSystemProperties.SourceId, clientId);

                // TODO remove this once akri service is code gen'd to expect srcId instead of invId
                requestMessage.AddUserProperty(AkriSystemProperties.CommandInvokerId, clientId);

                string timestamp = await _applicationContext.ApplicationHlc.UpdateNowAsync(cancellationToken: cancellationToken);
                requestMessage.AddUserProperty(AkriSystemProperties.Timestamp, timestamp);
                if (metadata != null)
                {
                    metadata.Timestamp = new HybridLogicalClock(_applicationContext.ApplicationHlc);
                }
                SerializedPayloadContext payloadContext = _serializer.ToBytes(request);
                if (!payloadContext.SerializedPayload.IsEmpty)
                {
                    requestMessage.Payload = payloadContext.SerializedPayload;
                    requestMessage.PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext.PayloadFormatIndicator;
                    requestMessage.ContentType = payloadContext.ContentType;
                }

                try
                {
                    metadata?.MarshalTo(requestMessage);
                }
                catch (AkriMqttException ex)
                {
                    throw AkriMqttException.GetArgumentInvalidException(_commandName, nameof(metadata), ex.HeaderName ?? string.Empty, ex.Message);
                }

                await SubscribeAsNeededAsync(responseTopicFilter, cancellationToken).ConfigureAwait(false);

                try
                {
                    MqttClientPublishResult pubAck = await _mqttClient.PublishAsync(requestMessage, cancellationToken).ConfigureAwait(false);
                    MqttClientPublishReasonCode pubReasonCode = pubAck.ReasonCode;
                    if (pubReasonCode != MqttClientPublishReasonCode.Success)
                    {
                        throw new AkriMqttException($"Command '{_commandName}' invocation failed due to an unsuccessful publishing with the error code {pubReasonCode}.")
                        {
                            Kind = AkriMqttErrorKind.MqttError,
                            InApplication = false,
                            IsShallow = false,
                            IsRemote = false,
                            CommandName = _commandName,
                            CorrelationId = requestGuid,
                        };
                    }
                    Trace.TraceInformation($"Invoked command '{_commandName}' with correlation ID {requestGuid} to topic '{requestTopic}'");
                }
                catch (Exception ex) when (ex is not AkriMqttException)
                {
                    throw new AkriMqttException($"Command '{_commandName}' invocation failed due to an exception thrown by MQTT Publish.", ex)
                    {
                        Kind = AkriMqttErrorKind.MqttError,
                        InApplication = false,
                        IsShallow = false,
                        IsRemote = false,
                        CommandName = _commandName,
                        CorrelationId = requestGuid,
                    };
                }

                ExtendedResponse<TResp> extendedResponse;
                try
                {
                    extendedResponse = await WallClock.WaitAsync(responsePromise.CompletionSource.Task, reifiedCommandTimeout, cancellationToken).ConfigureAwait(false);
                    if (responsePromise.CompletionSource.Task.IsFaulted)
                    {
                        throw responsePromise.CompletionSource.Task.Exception?.InnerException
                            ?? new AkriMqttException($"Command '{_commandName}' failed with unknown exception")
                            {
                                Kind = AkriMqttErrorKind.UnknownError,
                                InApplication = false,
                                IsShallow = false,
                                IsRemote = false,
                                CommandName = _commandName,
                                CorrelationId = requestGuid,
                            };
                    }
                }
                catch (TimeoutException e)
                {
                    SetCanceledSafe(responsePromise.CompletionSource);

                    throw new AkriMqttException($"Command '{_commandName}' timed out while waiting for a response", e)
                    {
                        Kind = AkriMqttErrorKind.Timeout,
                        InApplication = false,
                        IsShallow = false,
                        IsRemote = false,
                        TimeoutName = nameof(commandTimeout),
                        TimeoutValue = reifiedCommandTimeout,
                        CommandName = _commandName,
                        CorrelationId = requestGuid,
                    };
                }
                catch (OperationCanceledException e)
                {
                    SetCanceledSafe(responsePromise.CompletionSource);

                    throw new AkriMqttException($"Command '{_commandName}' was cancelled while waiting for a response", e)
                    {
                        Kind = AkriMqttErrorKind.Cancellation,
                        InApplication = false,
                        IsShallow = false,
                        IsRemote = false,
                        CommandName = _commandName,
                        CorrelationId = requestGuid,
                    };
                }

                return extendedResponse;
            }
            catch (ArgumentException ex)
            {
                throw new AkriMqttException(ex.Message)
                {
                    Kind = AkriMqttErrorKind.ArgumentInvalid,
                    InApplication = false,
                    IsShallow = true,
                    IsRemote = false,
                    PropertyName = ex.ParamName,
                    CommandName = _commandName,
                    CorrelationId = requestGuid,
                };
            }
            finally
            {
                // TODO #208
                //    completionSource.Task.Dispose();
                lock (_requestIdMapLock)
                {
                    _requestIdMap.Remove(requestGuid.ToString());
                }
            }
        }

        /// <summary>
        /// Dispose this object and the underlying mqtt client.
        /// </summary>
        /// <remarks>To avoid disposing the underlying mqtt client, use <see cref="DisposeAsync(bool)"/>.</remarks>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore(false).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose this object and choose whether to dispose the underlying mqtt client as well.
        /// </summary>
        /// <param name="disposing">
        /// If true, this call will dispose the underlying mqtt client. If false, this call will 
        /// not dispose the underlying mqtt client.
        /// </param>
        public async ValueTask DisposeAsync(bool disposing)
        {
            await DisposeAsyncCore(disposing).ConfigureAwait(false);
#pragma warning disable CA1816 // Call GC.SuppressFinalize correctly
            GC.SuppressFinalize(this);
#pragma warning restore CA1816 // Call GC.SuppressFinalize correctly
        }

        protected virtual async ValueTask DisposeAsyncCore(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            _mqttClient.ApplicationMessageReceivedAsync -= MessageReceivedCallbackAsync;

            lock (_requestIdMapLock)
            {
                foreach (KeyValuePair<string, ResponsePromise> responsePromise in _requestIdMap)
                {
                    if (responsePromise.Value != null && responsePromise.Value.CompletionSource != null)
                    {
                        SetCanceledSafe(responsePromise.Value.CompletionSource);
                    }
                }
                _requestIdMap.Clear();
            }

            try
            {
                if (_subscribedTopics.Count > 0)
                {
                    MqttClientUnsubscribeOptions unsubscribeOptions = new();
                    lock (_subscribedTopicsSetLock)
                    {
                        foreach (string subscribedTopic in _subscribedTopics)
                        {
                            unsubscribeOptions.TopicFilters.Add(subscribedTopic);
                        }
                    }

                    MqttClientUnsubscribeResult unsubAck = await _mqttClient.UnsubscribeAsync(unsubscribeOptions, CancellationToken.None).ConfigureAwait(false);
                    if (!unsubAck.IsUnsubAckSuccessful())
                    {
                        Trace.TraceError($"Failed to unsubscribe from the topic(s) for the command invoker of '{_commandName}'.");
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceWarning("Encountered an error while unsubscribing during disposal {0}", e);
            }

            lock (_subscribedTopicsSetLock)
            {
                _subscribedTopics.Clear();
            }

            if (disposing)
            {
                // This will disconnect and dispose the client if necessary
                await _mqttClient.DisposeAsync();
            }

            _isDisposed = true;
        }

        private static void SetExceptionSafe(TaskCompletionSource<ExtendedResponse<TResp>> tcs, Exception ex)
        {
            if (!tcs.TrySetException(ex))
            {
                Trace.TraceWarning("Failed to mark the command response promise as finished with exception. This may be because the operation was cancelled or already finished. Exception: {0}", ex);
            }
        }

        private static void SetCanceledSafe(TaskCompletionSource<ExtendedResponse<TResp>> tcs)
        {
            if (!tcs.TrySetCanceled())
            {
                Trace.TraceWarning($"Failed to cancel the response promise. This may be because the promise was already completed.");
            }
        }

        private class ResponsePromise(string responseTopic)
        {
            public string ResponseTopic { get; } = responseTopic;

            public TaskCompletionSource<ExtendedResponse<TResp>> CompletionSource { get; } = new TaskCompletionSource<ExtendedResponse<TResp>>();
        }
    }
}
