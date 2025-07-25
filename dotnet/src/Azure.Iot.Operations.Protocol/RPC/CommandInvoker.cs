﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;

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

        private readonly object _subscribedTopicsSetLock = new();
        private readonly HashSet<string> _subscribedTopics;

        private readonly object _requestIdMapLock = new();
        private readonly Dictionary<string, ResponsePromise> _requestIdMap;

        /// <summary>
        /// The topic token replacement map that this command invoker will use by default. Generally, this will include the token values
        /// for topic tokens such as "modelId" which should be the same for the duration of this command invoker's lifetime.
        /// </summary>
        /// <remarks>
        /// Tokens replacement values can also be specified per-method invocation by specifying the additionalTopicToken map in <see cref="InvokeCommandAsync(TReq, CommandRequestMetadata?, Dictionary{string, string}?, TimeSpan?, CancellationToken)"/>.
        /// </remarks>
        public Dictionary<string, string> TopicTokenMap { get; protected set; }

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

        private readonly ApplicationContext _applicationContext;

        public CommandInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer serializer)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _applicationContext = applicationContext;
            if (commandName == null || commandName == string.Empty)
            {
                throw AkriMqttException.GetConfigurationInvalidException(nameof(commandName), string.Empty);
            }

            _mqttClient = mqttClient ?? throw AkriMqttException.GetConfigurationInvalidException(commandName, nameof(mqttClient), string.Empty);
            _commandName = commandName;
            _serializer = serializer ?? throw AkriMqttException.GetConfigurationInvalidException(commandName, nameof(serializer), string.Empty);

            _subscribedTopics = [];
            _requestIdMap = [];

            RequestTopicPattern = AttributeRetriever.GetAttribute<CommandTopicAttribute>(this)?.RequestTopic ?? string.Empty;

            _mqttClient.ApplicationMessageReceivedAsync += MessageReceivedCallbackAsync;
            TopicTokenMap = new();
        }

        private string GenerateResponseTopicPattern(IReadOnlyDictionary<string, string>? combinedTopicTokenMap)
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
                PatternValidity patternValidity = MqttTopicProcessor.ValidateTopicPattern(ResponseTopicPrefix, combinedTopicTokenMap, requireReplacement: true, out string errMsg, out string? errToken, out string? errReplacement);
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
                PatternValidity patternValidity = MqttTopicProcessor.ValidateTopicPattern(ResponseTopicSuffix, combinedTopicTokenMap, requireReplacement: true, out string errMsg, out string? errToken, out string? errReplacement);
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

        private string GetCommandTopic(string pattern, Dictionary<string, string>? topicTokenMap = null)
        {
            topicTokenMap ??= new();
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

            commandTopic.Append(MqttTopicProcessor.ResolveTopic(pattern, topicTokenMap));

            return commandTopic.ToString();
        }

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

        private Task MessageReceivedCallbackAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            if (args.ApplicationMessage.CorrelationData != null && GuidExtensions.TryParseBytes(args.ApplicationMessage.CorrelationData, out Guid? requestGuid))
            {
                string requestGuidString = requestGuid!.Value.ToString();
                ResponsePromise? responsePromise;
                lock (_requestIdMapLock)
                {
                    if (!_requestIdMap.TryGetValue(requestGuidString, out responsePromise))
                    {
                        return Task.CompletedTask;
                    }
                }

                args.AutoAcknowledge = true;
                if (MqttTopicProcessor.DoesTopicMatchFilter(args.ApplicationMessage.Topic, responsePromise.ResponseTopic))
                {
                    responsePromise.Responses.Enqueue(args.ApplicationMessage);
                }
            }

            return Task.CompletedTask;
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
                CommandStatusCode.NotSupportedVersion => AkriMqttErrorKind.UnsupportedVersion,
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

        /// <summary>
        /// Invoke the specified command.
        /// </summary>
        /// <param name="request">The payload of command request.</param>
        /// <param name="metadata">The metadata of the command request.</param>
        /// <param name="additionalTopicTokenMap">
        /// The topic token replacement map to use in addition to <see cref="TopicTokenMap"/>. If this map
        /// contains any keys that <see cref="TopicTokenMap"/> also has, then values specified in this map will take precedence.
        /// </param>
        /// <param name="commandTimeout">How long to wait for a command response. Note that each command executor also has a configurable timeout value that may be shorter than this value. <see cref="CommandExecutor{TReq, TResp}.ExecutionTimeout"/></param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The command response including the command response metadata</returns>
        public async Task<ExtendedResponse<TResp>> InvokeCommandAsync(TReq request, CommandRequestMetadata? metadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
        {
            IAsyncEnumerable<StreamingExtendedResponse<TResp>> response = InvokeCommandAsync(false, request, metadata, additionalTopicTokenMap, commandTimeout, cancellationToken);
            var enumerator = response.GetAsyncEnumerator(cancellationToken);
            await enumerator.MoveNextAsync();
            return enumerator.Current;
        }

        public IAsyncEnumerable<StreamingExtendedResponse<TResp>> InvokeStreamingCommandAsync(TReq request, CommandRequestMetadata? metadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
        {
            // user shouldn't have to do the stitching. We do it. Ordering concerns, though?
            return InvokeCommandAsync(true, request, metadata, additionalTopicTokenMap, commandTimeout, cancellationToken);
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<TResp>> InvokeCommandAsync(bool isStreaming, TReq request, CommandRequestMetadata? metadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
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
                    IsShallow = true,
                    IsRemote = false,
                    CommandName = _commandName,
                    CorrelationId = requestGuid,
                };
            }

            Dictionary<string, string> combinedTopicTokenMap = new(TopicTokenMap);

            additionalTopicTokenMap ??= new();
            foreach (string topicTokenKey in additionalTopicTokenMap.Keys)
            {
                combinedTopicTokenMap.TryAdd(topicTokenKey, additionalTopicTokenMap[topicTokenKey]);
            }

            PatternValidity patternValidity = MqttTopicProcessor.ValidateTopicPattern(RequestTopicPattern, combinedTopicTokenMap, requireReplacement: true, out string errMsg, out string? errToken, out string? errReplacement);
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
                string requestTopic = GetCommandTopic(RequestTopicPattern, combinedTopicTokenMap);
                string responseTopicPattern = GenerateResponseTopicPattern(combinedTopicTokenMap);
                string responseTopic = GetCommandTopic(responseTopicPattern, combinedTopicTokenMap);
                string responseTopicFilter = GetCommandTopic(responseTopicPattern, TopicTokenMap);

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

                if (isStreaming)
                {
                    requestMessage.AddUserProperty(AkriSystemProperties.IsStreamingCommand, "true");
                }

                // TODO remove this once akri service is code gen'd to expect srcId instead of invId
                requestMessage.AddUserProperty(AkriSystemProperties.CommandInvokerId, clientId);

                string timestamp = await _applicationContext.ApplicationHlc.UpdateNowAsync(cancellationToken: cancellationToken);
                requestMessage.AddUserProperty(AkriSystemProperties.Timestamp, timestamp);
                await using var hlcClone = new HybridLogicalClock(_applicationContext.ApplicationHlc);
                if (metadata != null)
                {
                    metadata.Timestamp = hlcClone;
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
                        IsShallow = false,
                        IsRemote = false,
                        CommandName = _commandName,
                        CorrelationId = requestGuid,
                    };
                }

                //TODO operationCancelled and timeout exceptions were deleted to accomodate IAsyncEnumerable. Catch them elsewhere?
                // https://github.com/dotnet/roslyn/issues/39583#issuecomment-728097630 workaround?
                StreamingExtendedResponse<TResp> extendedResponse;

                // "do while" since every command should have at least one intended response, but streaming commands may have more
                do
                {
                    MqttApplicationMessage mqttMessage = await WallClock.WaitAsync<MqttApplicationMessage>(responsePromise.Responses.DequeueAsync(cancellationToken), reifiedCommandTimeout, cancellationToken).ConfigureAwait(false);

                    //TODO mqtt message to command response

                    // Assume a protocol version of 1.0 if no protocol version was specified
                    string? responseProtocolVersion = mqttMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.ProtocolVersion)?.Value;
                    if (!ProtocolVersion.TryParseProtocolVersion(responseProtocolVersion, out ProtocolVersion? protocolVersion))
                    {
                        AkriMqttException akriException = new($"Received a response with an unparsable protocol version number: {responseProtocolVersion}")
                        {
                            Kind = AkriMqttErrorKind.UnsupportedVersion,
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
                            Kind = AkriMqttErrorKind.UnsupportedVersion,
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

                    MqttUserProperty? statusProperty = mqttMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.Status);

                    if (!TryValidateResponseHeaders(statusProperty, requestGuidString, out AkriMqttErrorKind errorKind, out string message, out string? headerName, out string? headerValue))
                    {
                        AkriMqttException akriException = new(message)
                        {
                            Kind = errorKind,
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
                        MqttUserProperty? invalidNameProperty = mqttMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.InvalidPropertyName);
                        MqttUserProperty? invalidValueProperty = mqttMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.InvalidPropertyValue);
                        bool isApplicationError = (mqttMessage.UserProperties?.TryGetProperty(AkriSystemProperties.IsApplicationError, out string? isAppError) ?? false) && isAppError?.ToLower(CultureInfo.InvariantCulture) != "false";
                        string? statusMessage = mqttMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.StatusMessage)?.Value;

                        errorKind = StatusCodeToErrorKind((CommandStatusCode)statusCode, isApplicationError, invalidNameProperty != null, invalidValueProperty != null);
                        AkriMqttException akriException = new(statusMessage ?? "Error condition identified by remote service")
                        {
                            Kind = errorKind,
                            IsShallow = false,
                            IsRemote = true,
                            HeaderName = UseHeaderFields(errorKind) ? invalidNameProperty?.Value : null,
                            HeaderValue = UseHeaderFields(errorKind) ? invalidValueProperty?.Value : null,
                            PropertyName = UsePropertyFields(errorKind) ? invalidNameProperty?.Value : null,
                            PropertyValue = UsePropertyFields(errorKind) ? invalidValueProperty?.Value : null,
                            TimeoutName = UseTimeoutFields(errorKind) ? invalidNameProperty?.Value : null,
                            TimeoutValue = UseTimeoutFields(errorKind) ? GetAsTimeSpan(invalidValueProperty?.Value) : null,
                            CommandName = _commandName,
                            CorrelationId = requestGuid,
                        };

                        if (errorKind == AkriMqttErrorKind.UnsupportedVersion)
                        {
                            MqttUserProperty? supportedMajorVersions = mqttMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.SupportedMajorProtocolVersions);
                            MqttUserProperty? requestProtocolVersion = mqttMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.RequestedProtocolVersion);

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
                        response = _serializer.FromBytes<TResp>(mqttMessage.Payload, mqttMessage.ContentType, .PayloadFormatIndicator);
                        responseMetadata = new CommandResponseMetadata(mqttMessage);
                    }
                    catch (Exception ex)
                    {
                        SetExceptionSafe(responsePromise.CompletionSource, ex);
                        return;
                    }

                    if (responseMetadata.Timestamp != null)
                    {
                        await _applicationContext.ApplicationHlc.UpdateWithOtherAsync(responseMetadata.Timestamp, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        Trace.TraceInformation($"No timestamp present in command response metadata.");
                    }

                    extendedResponse = new() { Response = response, ResponseMetadata = responseMetadata };

                    if (!responsePromise.CompletionSource.TrySetResult(extendedResponse))
                    {
                        Trace.TraceWarning("Failed to complete the command response promise. This may be because the operation was cancelled or finished with exception.");
                    }

                    yield return extendedResponse;
                } while (extendedResponse.StreamingResponseId != null && !extendedResponse.IsLastResponse);

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

        private sealed class ResponsePromise(string responseTopic)
        {
            public string ResponseTopic { get; } = responseTopic;

            public BlockingConcurrentQueue<MqttApplicationMessage> Responses { get; } = new();
        }
    }
}
