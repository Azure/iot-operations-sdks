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
        private const int majorProtocolVersion = 1;
        private const int minorProtocolVersion = 0;

        private int[] supportedMajorProtocolVersions = [1];

        private const string? DefaultResponseTopicPrefix = $"clients/{MqttTopicTokens.CommandInvokerId}";
        private const string? DefaultResponseTopicSuffix = null;
        private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MinimumCommandTimeout = TimeSpan.FromMilliseconds(1);

        internal static IWallClock WallClock = new WallClock();

        private readonly IMqttPubSubClient mqttClient;
        private readonly string commandName;
        private readonly IPayloadSerializer serializer;

        private readonly object subscribedTopicsSetLock = new();
        private readonly HashSet<string> subscribedTopics;

        private readonly object requestIdMapLock = new();
        private readonly Dictionary<string, ResponsePromise> requestIdMap;

        private string? topicNamespace;

        private string? responseTopicPrefix;

        private string? responseTopicSuffix;

        private bool isDisposed;

        public string ModelId { get; init; }

        public string RequestTopicPattern { get; init; }

        public Dictionary<string, string>? CustomTopicTokenMap { get; init; }

        public string? Partition { get; private init; }

        public string? TopicNamespace
        {
            get => topicNamespace;
            set
            {
                ObjectDisposedException.ThrowIf(isDisposed, this);
                if (value != null && !MqttTopicProcessor.IsValidReplacement(value))
                {
                    throw AkriMqttException.GetConfigurationInvalidException(nameof(TopicNamespace), value, "MQTT topic namespace is not valid", commandName: commandName);
                }

                topicNamespace = value;
            }
        }

        public string? ResponseTopicPrefix
        {
            get => responseTopicPrefix;
            init
            {
                if (value != null)
                {
                    try
                    {
                        MqttTopicProcessor.ValidateCommandTopicPattern(value, nameof(ResponseTopicPrefix), commandName, ModelId, CustomTopicTokenMap);
                    }
                    catch (ArgumentException ex)
                    {
                        throw AkriMqttException.GetConfigurationInvalidException(nameof(ResponseTopicPrefix), value, ex.Message, ex, commandName);
                    }
                }

                responseTopicPrefix = value;
            }
        }

        public string? ResponseTopicSuffix
        {
            get => responseTopicSuffix;
            init
            {
                if (value != null)
                {
                    try
                    {
                        MqttTopicProcessor.ValidateCommandTopicPattern(value, nameof(ResponseTopicSuffix), commandName, ModelId, CustomTopicTokenMap);
                    }
                    catch (ArgumentException ex)
                    {
                        throw AkriMqttException.GetConfigurationInvalidException(nameof(ResponseTopicSuffix), value, ex.Message, ex, commandName);
                    }
                }

                responseTopicSuffix = value;
            }
        }

        public Func<string, string>? GetResponseTopic { get; init; }

        public CommandInvoker(IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer serializer)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);

            if (commandName == null || commandName == string.Empty)
            {
                throw AkriMqttException.GetArgumentInvalidException(string.Empty, nameof(commandName), string.Empty);
            }

            this.mqttClient = mqttClient ?? throw AkriMqttException.GetArgumentInvalidException(commandName, nameof(mqttClient), string.Empty);
            this.commandName = commandName;
            this.serializer = serializer ?? throw AkriMqttException.GetArgumentInvalidException(commandName, nameof(serializer), string.Empty);

            // default Partition property to the client id
            this.Partition = mqttClient.ClientId;

            subscribedTopics = new();
            requestIdMap = new();

            ModelId = AttributeRetriever.GetAttribute<ModelIdAttribute>(this)?.Id ?? string.Empty;
            RequestTopicPattern = AttributeRetriever.GetAttribute<CommandTopicAttribute>(this)?.RequestTopic ?? string.Empty;
            CustomTopicTokenMap = null;
            topicNamespace = null;
            responseTopicPrefix = DefaultResponseTopicPrefix;
            responseTopicSuffix = DefaultResponseTopicSuffix;

            GetResponseTopic = null;

            this.mqttClient.ApplicationMessageReceivedAsync += MessageReceivedCallbackAsync;
        }

        private string GenerateResponseTopicPattern()
        {
            StringBuilder responseTopicPattern = new();

            if (ResponseTopicPrefix != null)
            {
                responseTopicPattern.Append(ResponseTopicPrefix);
                responseTopicPattern.Append('/');
            }

            responseTopicPattern.Append(RequestTopicPattern);

            if (ResponseTopicSuffix != null)
            {
                responseTopicPattern.Append('/');
                responseTopicPattern.Append(ResponseTopicSuffix);
            }

            return responseTopicPattern.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="executorId"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private string GetCommandTopic(string pattern, string executorId)
        {
            StringBuilder commandTopic = new();

            if (topicNamespace != null)
            {
                commandTopic.Append(topicNamespace);
                commandTopic.Append('/');
            }

            commandTopic.Append(MqttTopicProcessor.GetCommandTopic(pattern, commandName, executorId, mqttClient.ClientId, ModelId, CustomTopicTokenMap));

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
            lock (subscribedTopicsSetLock)
            {
                if (subscribedTopics.Contains(responseTopicFilter))
                {
                    return;
                }
            }

            try
            {
                MqttTopicProcessor.ValidateCommandTopicPattern(RequestTopicPattern, nameof(RequestTopicPattern), commandName, ModelId, CustomTopicTokenMap);
            }
            catch (ArgumentException ex)
            {
                throw AkriMqttException.GetConfigurationInvalidException(nameof(RequestTopicPattern), RequestTopicPattern, ex.Message, ex, commandName);
            }

            if (mqttClient.ProtocolVersion != MqttProtocolVersion.V500)
            {
                throw AkriMqttException.GetConfigurationInvalidException(
                    "MQTTClient.ProtocolVersion",
                    mqttClient.ProtocolVersion,
                    "The provided MQTT client is not configured for MQTT version 5",
                    commandName: commandName);
            }

            var qos = MqttQualityOfServiceLevel.AtLeastOnce;
            MqttClientSubscribeOptions mqttSubscribeOptions = new MqttClientSubscribeOptions(responseTopicFilter, qos);
            
            MqttClientSubscribeResult subAck = await mqttClient.SubscribeAsync(mqttSubscribeOptions, cancellationToken).ConfigureAwait(false);
            subAck.ThrowIfNotSuccessSubAck(qos, this.commandName);

            lock (subscribedTopicsSetLock)
            {
                subscribedTopics.Add(responseTopicFilter);
            }
        }

        private Task MessageReceivedCallbackAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            if (args.ApplicationMessage.CorrelationData != null && GuidExtensions.TryParseBytes(args.ApplicationMessage.CorrelationData, out Guid? requestGuid))
            {
                string requestGuidString = requestGuid!.Value.ToString();
                ResponsePromise? responsePromise;
                lock (requestIdMapLock)
                {
                    if (!requestIdMap.TryGetValue(requestGuidString, out responsePromise))
                    {
                        return Task.CompletedTask;
                    }
                }

                args.AutoAcknowledge = true;
                if (MqttTopicProcessor.DoesTopicMatchFilter(args.ApplicationMessage.Topic, responsePromise.ResponseTopic))
                {
                    // Assume a protocol version of 1.0 if no protocol version was specified
                    string? responseProtocolVersion = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.ProtocolVersion)?.Value ?? "1.0";
                    if (!ProtocolVersion.TryParseProtocolVersion(responseProtocolVersion, out ProtocolVersion? protocolVersion))
                    {
                        var akriException = new AkriMqttException($"Received a response with an unparsable protocol version number: {responseProtocolVersion}")
                        {
                            Kind = AkriMqttErrorKind.UnsupportedResponseVersion,
                            InApplication = false,
                            IsShallow = false,
                            IsRemote = false,
                            CommandName = commandName,
                            CorrelationId = requestGuid,
                            SupportedMajorProtocolVersions = supportedMajorProtocolVersions,
                            ProtocolVersion = responseProtocolVersion,
                        };

                        SetExceptionSafe(responsePromise.CompletionSource, akriException);
                        return Task.CompletedTask;
                    }

                    if (!supportedMajorProtocolVersions.Contains(protocolVersion!.MajorVersion))
                    {
                        var akriException = new AkriMqttException($"Received a response with an unsupported protocol version number: {responseProtocolVersion}")
                        {
                            Kind = AkriMqttErrorKind.UnsupportedResponseVersion,
                            InApplication = false,
                            IsShallow = false,
                            IsRemote = false,
                            CommandName = commandName,
                            CorrelationId = requestGuid,
                            SupportedMajorProtocolVersions = supportedMajorProtocolVersions,
                            ProtocolVersion = responseProtocolVersion,
                        };

                        SetExceptionSafe(responsePromise.CompletionSource, akriException);
                        return Task.CompletedTask;
                    }

                    MqttUserProperty? statusProperty = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.Status);

                    if (!TryValidateResponseHeaders(args.ApplicationMessage, statusProperty, requestGuidString, out AkriMqttErrorKind errorKind, out string message, out string? headerName, out string? headerValue))
                    {
                        AkriMqttException akriException = new AkriMqttException(message)
                        {
                            Kind = errorKind,
                            InApplication = false,
                            IsShallow = false,
                            IsRemote = false,
                            HeaderName = headerName,
                            HeaderValue = headerValue,
                            CommandName = commandName,
                            CorrelationId = requestGuid,
                        };

                        SetExceptionSafe(responsePromise.CompletionSource, akriException);
                        return Task.CompletedTask;
                    }

                    int statusCode = int.Parse(statusProperty!.Value, CultureInfo.InvariantCulture);

                    if (statusCode != (int)CommandStatusCode.OK && statusCode != (int)CommandStatusCode.NoContent)
                    {
                        MqttUserProperty? invalidNameProperty = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.InvalidPropertyName);
                        MqttUserProperty? invalidValueProperty = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.InvalidPropertyValue);
                        bool isApplicationError = (args.ApplicationMessage.UserProperties?.TryGetProperty(AkriSystemProperties.IsApplicationError, out string? isAppError) ?? false) && isAppError?.ToLower(CultureInfo.InvariantCulture) != "false";
                        string? statusMessage = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.StatusMessage)?.Value;

                        errorKind = StatusCodeToErrorKind((CommandStatusCode)statusCode, isApplicationError, invalidNameProperty != null, invalidValueProperty != null);
                        AkriMqttException akriException = new AkriMqttException(statusMessage ?? "Error condition identified by remote service")
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
                            CommandName = commandName,
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
                        return Task.CompletedTask;
                    }

                    TResp response;
                    CommandResponseMetadata responseMetadata;
                    try
                    {
                        response = serializer.FromBytes<TResp>(args.ApplicationMessage.PayloadSegment.Array);
                        responseMetadata = new CommandResponseMetadata(args.ApplicationMessage);
                    }
                    catch (Exception ex)
                    {
                        SetExceptionSafe(responsePromise.CompletionSource, ex);
                        return Task.CompletedTask;
                    }

                    ExtendedResponse<TResp> extendedResponse = new ExtendedResponse<TResp> { Response = response, ResponseMetadata = responseMetadata };

                    if (!responsePromise.CompletionSource.TrySetResult(extendedResponse))
                    {
                        Trace.TraceWarning("Failed to complete the command response promise. This may be because the operation was cancelled or finished with exception.");
                    }
                }
            }

            return Task.CompletedTask;
        }

        private bool TryValidateResponseHeaders(
            MqttApplicationMessage responseMsg,
            MqttUserProperty? statusProperty,
            string correlationId,
            out AkriMqttErrorKind errorKind,
            out string message,
            out string? headerName,
            out string? headerValue)
        {
            if (responseMsg.ContentType != null && responseMsg.ContentType != this.serializer.ContentType)
            {
                errorKind = AkriMqttErrorKind.HeaderInvalid;
                message = $"Content type {responseMsg.ContentType} is not supported by this implementation; only {this.serializer.ContentType} is accepted.";
                headerName = "Content Type";
                headerValue = responseMsg.ContentType;
                return false;
            }

            if (responseMsg.PayloadFormatIndicator != MqttPayloadFormatIndicator.Unspecified && (int)responseMsg.PayloadFormatIndicator != this.serializer.CharacterDataFormatIndicator)
            {
                errorKind = AkriMqttErrorKind.HeaderInvalid;
                message = $"Format indicator {responseMsg.PayloadFormatIndicator} is not appropriate for {this.serializer.ContentType} content.";
                headerName = "Payload Format Indicator";
                headerValue = ((int)responseMsg.PayloadFormatIndicator).ToString(CultureInfo.InvariantCulture);
                return false;
            }

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

            if (!int.TryParse(statusProperty.Value, out int statusCode))
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
                CommandStatusCode.UnprocessableContent => AkriMqttErrorKind.InvocationException,
                CommandStatusCode.InternalServerError =>
                    isAppError ? AkriMqttErrorKind.ExecutionException :
                    hasInvalidName ? AkriMqttErrorKind.InternalLogicError :
                    AkriMqttErrorKind.UnknownError,
                CommandStatusCode.NotSupportedVersion => AkriMqttErrorKind.UnsupportedRequestVersion,
                CommandStatusCode.ServiceUnavailable => AkriMqttErrorKind.StateInvalid,
                _ => AkriMqttErrorKind.UnknownError,
            };
        }

        private static bool UseHeaderFields(AkriMqttErrorKind errorKind) => errorKind == AkriMqttErrorKind.HeaderMissing || errorKind == AkriMqttErrorKind.HeaderInvalid;

        private static bool UseTimeoutFields(AkriMqttErrorKind errorKind) => errorKind == AkriMqttErrorKind.Timeout;

        private static bool UsePropertyFields(AkriMqttErrorKind errorKind) => !UseHeaderFields(errorKind) && !UseTimeoutFields(errorKind);

        private static TimeSpan? GetAsTimeSpan(string? value) => value != null ? XmlConvert.ToTimeSpan(value) : null;

        public async Task<ExtendedResponse<TResp>> InvokeCommandAsync(string executorId, TReq request, CommandRequestMetadata? metadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(isDisposed, this);

            Guid requestGuid = metadata?.CorrelationId ?? Guid.NewGuid();

            TimeSpan reifiedCommandTimeout = commandTimeout ?? DefaultCommandTimeout;

            if (reifiedCommandTimeout < MinimumCommandTimeout)
            {
                throw AkriMqttException.GetConfigurationInvalidException("commandTimeout", reifiedCommandTimeout, $"commandTimeout must be at least {MinimumCommandTimeout}", commandName: commandName);
            }


            if (reifiedCommandTimeout.TotalSeconds > uint.MaxValue)
            {
                throw AkriMqttException.GetConfigurationInvalidException("commandTimeout", reifiedCommandTimeout, $"commandTimeout cannot be larger than {uint.MaxValue} seconds");
            }
   
            if(requestIdMap.ContainsKey(requestGuid.ToString()))
			{
				throw new AkriMqttException($"Command '{this.commandName}' invocation failed due to duplicate request with same correlationId")
				{
					Kind = AkriMqttErrorKind.StateInvalid,
					InApplication = false,
					IsShallow = true,
					IsRemote = false,
					CommandName = this.commandName,
					CorrelationId = requestGuid,
				};
			}         

            try
            {
                string? requestTopic = null;
                try
                {
                    requestTopic = GetCommandTopic(RequestTopicPattern, executorId);
                }
                catch (ArgumentException ex)
                {
                    throw ex.ParamName == nameof(executorId) ?
                        AkriMqttException.GetArgumentInvalidException(commandName, nameof(executorId), executorId, ex.Message) :
                        AkriMqttException.GetConfigurationInvalidException(nameof(RequestTopicPattern), RequestTopicPattern, ex.Message, ex, commandName);
                }
                string responseTopic = GetResponseTopic != null ? GetResponseTopic(requestTopic) : GetCommandTopic(GenerateResponseTopicPattern(), executorId);
                string responseTopicFilter = GetResponseTopic != null ? responseTopic : GetCommandTopic(GenerateResponseTopicPattern(), "+");

                ResponsePromise responsePromise = new(responseTopic);

                lock (requestIdMapLock)
                { 
                    requestIdMap[requestGuid.ToString()] = responsePromise;
                }

                var requestMessage = new MqttApplicationMessage(requestTopic, MqttQualityOfServiceLevel.AtLeastOnce)
                {
                    ResponseTopic = responseTopic,
                    CorrelationData = requestGuid.ToByteArray(),
                    MessageExpiryInterval = (uint)reifiedCommandTimeout.TotalSeconds,
                };

                requestMessage.AddUserProperty(AkriSystemProperties.CommandInvokerId, mqttClient.ClientId);
                requestMessage.AddUserProperty(AkriSystemProperties.ProtocolVersion, $"{majorProtocolVersion}.{minorProtocolVersion}");

                if (Partition != null)
                {
                    requestMessage.AddUserProperty("$partition", Partition);
                }

                byte[]? payload = serializer.ToBytes(request);
                if (payload != null)
                {
                    requestMessage.PayloadSegment = payload;
                    requestMessage.PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator;
                    requestMessage.ContentType = serializer.ContentType;
                }

                try
                {
                    metadata?.MarshalTo(requestMessage);
                }
                catch (AkriMqttException ex)
                {
                    throw AkriMqttException.GetArgumentInvalidException(commandName, nameof(metadata), ex.HeaderName ?? string.Empty, ex.Message);
                }

                await SubscribeAsNeededAsync(responseTopicFilter, cancellationToken).ConfigureAwait(false);

                try
                {
                    MqttClientPublishResult pubAck = await mqttClient.PublishAsync(requestMessage, cancellationToken).ConfigureAwait(false);
                    var pubReasonCode = pubAck.ReasonCode;
                    if (pubReasonCode != MqttClientPublishReasonCode.Success)
                    {
                        throw new AkriMqttException($"Command '{this.commandName}' invocation failed due to an unsuccessful publishing with the error code {pubReasonCode}.")
                        {
                            Kind = AkriMqttErrorKind.MqttError,
                            InApplication = false,
                            IsShallow = false,
                            IsRemote = false,
                            CommandName = this.commandName,
                            CorrelationId = requestGuid,
                        };
                    }
                }
                catch (Exception ex) when (ex is not AkriMqttException)
                {
                    throw new AkriMqttException($"Command '{this.commandName}' invocation failed due to an exception thrown by MQTT Publish.", ex)
                    {
                        Kind = AkriMqttErrorKind.MqttError,
                        InApplication = false,
                        IsShallow = false,
                        IsRemote = false,
                        CommandName = this.commandName,
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
                            ?? new AkriMqttException($"Command '{commandName}' failed with unknown exception")
                            {
                                Kind = AkriMqttErrorKind.UnknownError,
                                InApplication = false,
                                IsShallow = false,
                                IsRemote = false,
                                CommandName = commandName,
                                CorrelationId = requestGuid,
                            };
                    }
                }
                catch (TimeoutException e)
                {
                    string fromWhere = executorId != string.Empty ? $" from command server {executorId}" : string.Empty;
                    SetCanceledSafe(responsePromise.CompletionSource);

                    throw new AkriMqttException($"Command '{commandName}' timed out while waiting for a response {fromWhere}", e)
                    {
                        Kind = AkriMqttErrorKind.Timeout,
                        InApplication = false,
                        IsShallow = false,
                        IsRemote = false,
                        TimeoutName = nameof(commandTimeout),
                        TimeoutValue = reifiedCommandTimeout,
                        CommandName = commandName,
                        CorrelationId = requestGuid,
                    };
                }
                catch (OperationCanceledException e)
                {
                    string fromWhere = executorId != string.Empty ? $" from command server {executorId}" : string.Empty;
                    SetCanceledSafe(responsePromise.CompletionSource);

                    throw new AkriMqttException($"Command '{commandName}' was cancelled while waiting for a response {fromWhere}", e)
                    {
                        Kind = AkriMqttErrorKind.Cancellation,
                        InApplication = false,
                        IsShallow = false,
                        IsRemote = false,
                        CommandName = commandName,
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
                    CommandName = commandName,
                    CorrelationId = requestGuid,
                };
            }
            finally
            {
                // TODO #208
                //    completionSource.Task.Dispose();
                lock (requestIdMapLock)
                {
                    requestIdMap.Remove(requestGuid.ToString());
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
            if (isDisposed)
            {
                return;
            }

            mqttClient.ApplicationMessageReceivedAsync -= MessageReceivedCallbackAsync;

            lock (requestIdMapLock)
            {
                foreach (var responsePromise in requestIdMap)
                {
                    if (responsePromise.Value != null && responsePromise.Value.CompletionSource != null)
                    {
                        SetCanceledSafe(responsePromise.Value.CompletionSource);
                    }
                }
                requestIdMap.Clear();
            }

            try
            {
                if (subscribedTopics.Count > 0)
                {
                    MqttClientUnsubscribeOptions unsubscribeOptions = new MqttClientUnsubscribeOptions();
                    lock (subscribedTopicsSetLock)
                    {
                        foreach (string subscribedTopic in subscribedTopics)
                        {
                            unsubscribeOptions.TopicFilters.Add(subscribedTopic);
                        }
                    }

                    MqttClientUnsubscribeResult unsubAck = await mqttClient.UnsubscribeAsync(unsubscribeOptions, CancellationToken.None).ConfigureAwait(false);
                    if (!unsubAck.IsUnsubAckSuccessful())
                    {
                        Trace.TraceError($"Failed to unsubscribe from the topic(s) for the command invoker of '{this.commandName}'.");
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceWarning("Encountered an error while unsubscribing during disposal {0}", e);
            }

            lock (subscribedTopicsSetLock)
            { 
                subscribedTopics.Clear();
            }

            if (disposing)
            {
                // This will disconnect and dispose the client if necessary
                await mqttClient.DisposeAsync();
            }
                
            isDisposed = true;
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

        private class ResponsePromise
        {
            public ResponsePromise(string responseTopic)
            {
                ResponseTopic = responseTopic;
                CompletionSource = new TaskCompletionSource<ExtendedResponse<TResp>>();
            }

            public string ResponseTopic { get; }

            public TaskCompletionSource<ExtendedResponse<TResp>> CompletionSource { get; }
        }
    }
}