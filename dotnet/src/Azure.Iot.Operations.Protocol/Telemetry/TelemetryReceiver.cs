﻿// Copyright (c) Microsoft Corporation.
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

                    IncomingTelemetryMetadata metadata = new(args.ApplicationMessage, args.PacketIdentifier, TopicPattern);

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

        public async Task StartAsync(Dictionary<string, string>? additionalTopicTokenMap = null, CancellationToken cancellationToken = default)
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

                Dictionary<string, string> combinedTopicTokenMap = new();
                foreach (string topicTokenKey in TopicTokenMap.Keys)
                {
                    combinedTopicTokenMap.TryAdd(topicTokenKey, TopicTokenMap[topicTokenKey]);
                }

                additionalTopicTokenMap ??= new();
                foreach (string topicTokenKey in additionalTopicTokenMap.Keys)
                {
                    combinedTopicTokenMap.TryAdd(topicTokenKey, additionalTopicTokenMap[topicTokenKey]);
                }

                PatternValidity patternValidity = MqttTopicProcessor.ValidateTopicPattern(TopicPattern, combinedTopicTokenMap, requireReplacement: false, out string errMsg, out string? errToken, out string? errReplacement);
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
                    Trace.TraceWarning("Failed to stop the telemetry receiver while disposing it: {0}", ex);
                }

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
