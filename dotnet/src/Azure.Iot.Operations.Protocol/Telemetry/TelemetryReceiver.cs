// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Telemetry
{
    public abstract class TelemetryReceiver<T> : IAsyncDisposable
        where T : class
    {
        private readonly ApplicationContext _applicationContext;
        private readonly int[] _supportedMajorProtocolVersions = [TelemetryVersion.MajorProtocolVersion];

        private static readonly int PreferredDispatchConcurrency = 10;
        private static readonly TimeSpan DefaultTelemetryTimeout = TimeSpan.FromSeconds(10);

        internal static IWallClock WallClock = new WallClock();

        private readonly IMqttPubSubClient _mqttClient;
        private readonly IPayloadSerializer _serializer;

        private Dispatcher? _dispatcher;

        private bool _isRunning;

        private bool _isDisposed;

        public Func<string, T, IncomingTelemetryMetadata, Task>? OnTelemetryReceived { get; set; }

        public string ServiceGroupId { get; init; }

        public string TopicPattern { get; init; }

        public string? TopicNamespace { get; set; }

        /// <summary>
        /// The topic token replacement map that this receiver will use by default. Generally, this will include the token values
        /// for topic tokens such as "modelId" which should be the same for the duration of this receiver's lifetime.
        /// </summary>
        /// <remarks>
        /// Tokens replacement values can also be specified when starting the receiver by specifying the additionalTopicToken map in <see cref="StartAsync(Dictionary{string, string}?, CancellationToken)"/>.
        /// </remarks>
        public Dictionary<string, string> TopicTokenMap { get; protected set; }

        public TelemetryReceiver(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, IPayloadSerializer serializer)
        {
            _applicationContext = applicationContext;
            _mqttClient = mqttClient;
            _serializer = serializer;

            _isRunning = false;

            OnTelemetryReceived = default;

            _dispatcher = null;

            ServiceGroupId = AttributeRetriever.GetAttribute<ServiceGroupIdAttribute>(this)?.Id ?? string.Empty;
            TopicPattern = AttributeRetriever.GetAttribute<TelemetryTopicAttribute>(this)?.Topic ?? string.Empty;

            mqttClient.ApplicationMessageReceivedAsync += MessageReceivedCallbackAsync;
            TopicTokenMap = new();
        }

        private async Task MessageReceivedCallbackAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            Trace.TraceInformation($"Telemetry received from {args.ApplicationMessage.Topic}");
            string telemTopicFilter = GetTelemetryTopic();

            if (MqttTopicProcessor.DoesTopicMatchFilter(args.ApplicationMessage.Topic, telemTopicFilter))
            {
                string? requestProtocolVersion = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.ProtocolVersion)?.Value;

                if (!ProtocolVersion.TryParseProtocolVersion(requestProtocolVersion, out ProtocolVersion? protocolVersion))
                {
                    Trace.TraceError($"Telemetry with CorrelationId {args.ApplicationMessage.CorrelationData} provided a malformed protocol version {requestProtocolVersion}. The telemetry will be ignored by this receiver.");
                    return;
                }

                if (!_supportedMajorProtocolVersions.Contains(protocolVersion!.MajorVersion))
                {
                    Trace.TraceError($"Telemetry with CorrelationId {args.ApplicationMessage.CorrelationData} requested an unsupported protocol version {requestProtocolVersion}. This telemetry reciever supports versions {ProtocolVersion.ToString(_supportedMajorProtocolVersions)}. The telemetry will be ignored by this receiver.");
                    return;
                }

                args.AutoAcknowledge = false;

                DateTime messageReceivedTime = WallClock.UtcNow;

                TimeSpan telemetryTimeout = args.ApplicationMessage.MessageExpiryInterval != default ? TimeSpan.FromSeconds(args.ApplicationMessage.MessageExpiryInterval) : DefaultTelemetryTimeout;
                DateTime telemetryExpirationTime = messageReceivedTime + telemetryTimeout;

                string sourceId = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.SourceId)?.Value ?? string.Empty;

                if (OnTelemetryReceived == null)
                {
                    await GetDispatcher()(null, async () => { await args.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
                    return;
                }

                try
                {
                    T serializedPayload = _serializer.FromBytes<T>(args.ApplicationMessage.Payload, args.ApplicationMessage.ContentType, args.ApplicationMessage.PayloadFormatIndicator);

                    IncomingTelemetryMetadata metadata = new(args.ApplicationMessage, args.PacketIdentifier, TopicPattern, TopicNamespace);

                    if (metadata.Timestamp != null)
                    {
                        // Update application HLC against received TS
                        await _applicationContext.ApplicationHlc.UpdateWithOtherAsync(metadata.Timestamp);
                    }
                    else
                    {
                        Trace.TraceInformation($"No timestamp present in telemetry received metadata.");
                    }

                    async Task TelemFunc()
                    {
                        try
                        {
                            await OnTelemetryReceived(sourceId, serializedPayload, metadata);
                        }
                        catch (Exception innerEx)
                        {
                            Trace.TraceError($"Exception thrown while executing telemetry received callback: {innerEx.Message}");
                        }
                    }
                    await GetDispatcher()(TelemFunc, async () => { await args.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
                }
                catch (Exception outerEx)
                {
                    await GetDispatcher()(null, async () => { await args.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
                    Trace.TraceError($"Exception thrown while deserializing payload, callback skipped: {outerEx.Message}");
                }
            }
        }

        /// <summary>
        /// Begin accepting telemetry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task StartAsync(CancellationToken cancellationToken = default)
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
                        "The provided MQTT client is not configured for MQTT version 5");
                }

                string? clientId = _mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before starting a telemetry receiver");
                }

                _dispatcher ??= ExecutionDispatcher.CollectionInstance.GetDispatcher(clientId, PreferredDispatchConcurrency);

                if (TopicNamespace != null && !MqttTopicProcessor.IsValidReplacement(TopicNamespace))
                {
                    throw AkriMqttException.GetConfigurationInvalidException(nameof(TopicNamespace), TopicNamespace, "MQTT topic namespace is not valid");
                }

                PatternValidity patternValidity = MqttTopicProcessor.ValidateTopicPattern(TopicPattern, TopicTokenMap, requireReplacement: false, out string errMsg, out string? errToken, out string? errReplacement);
                if (patternValidity != PatternValidity.Valid)
                {
                    throw patternValidity switch
                    {
                        PatternValidity.InvalidResidentReplacement => AkriMqttException.GetConfigurationInvalidException(errToken!, errReplacement!, errMsg),
                        _ => AkriMqttException.GetConfigurationInvalidException(nameof(TopicPattern), TopicPattern, errMsg),
                    };
                }

                string telemTopicFilter = ServiceGroupId != string.Empty ? $"$share/{ServiceGroupId}/{GetTelemetryTopic()}" : GetTelemetryTopic();

                MqttTopicFilter topicFilter = new(telemTopicFilter, MqttQualityOfServiceLevel.AtLeastOnce);

                MqttClientSubscribeOptions mqttSubscribeOptions = new(topicFilter);

                MqttClientSubscribeResult subAck = await _mqttClient.SubscribeAsync(mqttSubscribeOptions, cancellationToken).ConfigureAwait(false);
                subAck.ThrowIfNotSuccessSubAck(topicFilter.QualityOfServiceLevel);
                _isRunning = true;
                Trace.TraceInformation($"Telemetry receiver subscribed for topic {telemTopicFilter}.");
            }
        }

        /// <summary>
        /// Unsubscribe from any MQTT topics that this client subscribed to
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <remarks>
        /// This operation does require the underlying MQTT client to be connected to complete, so users are advised to pass in a cancellation token
        /// to protect against the case where the underlying MQTT client gets disconnected and takes an unexpectedly long time to reconnect.
        /// </remarks>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (_isRunning)
            {
                string telemTopicFilter = ServiceGroupId != string.Empty ? $"$share/{ServiceGroupId}/{GetTelemetryTopic()}" : GetTelemetryTopic();

                MqttClientUnsubscribeOptions unsubscribeOptions = new(telemTopicFilter);

                MqttClientUnsubscribeResult unsubAck = await _mqttClient.UnsubscribeAsync(unsubscribeOptions, cancellationToken).ConfigureAwait(false);
                unsubAck.ThrowIfNotSuccessUnsubAck();
                _isRunning = false;
                Trace.TraceInformation($"Telemetry receiver unsubscribed for topic {telemTopicFilter}.");
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

        private string GetTelemetryTopic()
        {
            StringBuilder telemTopic = new();

            if (TopicNamespace != null)
            {
                telemTopic.Append(TopicNamespace);
                telemTopic.Append('/');
            }

            telemTopic.Append(MqttTopicProcessor.ResolveTopic(TopicPattern, TopicTokenMap));

            return telemTopic.ToString();
        }

        /// <summary>
        /// Asynchronously dispose this object, but not the underlying mqtt client.
        /// </summary>
        /// <remarks>
        /// Users are advised to call <see cref="StopAsync(CancellationToken)"/> prior to this in order to cleanup any MQTT subscriptions that this client has.
        ///
        /// To also dispose the underlying mqtt client, use <see cref="DisposeAsync(bool)"/>.
        /// </remarks>
        public virtual async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore(false, CancellationToken.None);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronously dispose this object, but not the underlying mqtt client.
        /// </summary>
        /// <remarks>
        /// Users are advised to call <see cref="StopAsync(CancellationToken)"/> prior to this in order to cleanup any MQTT subscriptions that this client has.
        /// 
        /// To also dispose the underlying mqtt client, use <see cref="DisposeAsync(bool, CancellationToken)"/>.
        /// </remarks>
        public virtual async ValueTask DisposeAsync(CancellationToken cancellationToken)
        {
            await DisposeAsyncCore(false, cancellationToken);
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize. Reason: this is a dispose method
            GC.SuppressFinalize(this);
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        }

        /// <summary>
        /// Asynchronously dispose this object and optionally dispose the underlying mqtt client as well.
        /// </summary>
        /// <param name="disposing">
        /// If true, this call will dispose the underlying mqtt client. If false, this call will
        /// not dispose the underlying mqtt client.
        /// </param>
        /// <remarks>
        /// Users are advised to call <see cref="StopAsync(CancellationToken)"/> prior to this in order to cleanup any MQTT subscriptions that this client has.
        /// </remarks>
        public virtual async ValueTask DisposeAsync(bool disposing)
        {
            await DisposeAsyncCore(disposing, CancellationToken.None);
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize. Reason: this is a dispose method
            GC.SuppressFinalize(this);
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        }

        /// <summary>
        /// Asynchronously dispose this object and optionally dispose the underlying mqtt client as well.
        /// </summary>
        /// <param name="disposing">
        /// If true, this call will dispose the underlying mqtt client. If false, this call will
        /// not dispose the underlying mqtt client.
        /// </param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <remarks>
        /// Users are advised to call <see cref="StopAsync(CancellationToken)"/> prior to this in order to cleanup any MQTT subscriptions that this client has.
        /// </remarks>
        public virtual async ValueTask DisposeAsync(bool disposing, CancellationToken cancellationToken)
        {
            await DisposeAsyncCore(disposing, cancellationToken);
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize. Reason: this is a dispose method
            GC.SuppressFinalize(this);
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        }

        protected virtual async ValueTask DisposeAsyncCore(bool disposing, CancellationToken cancellationToken)
        {
            if (!_isDisposed)
            {
                _mqttClient.ApplicationMessageReceivedAsync -= MessageReceivedCallbackAsync;

                if (disposing)
                {
                    await _mqttClient.DisposeAsync(disposing, cancellationToken);
                }

                _isDisposed = true;
            }
        }
    }
}
