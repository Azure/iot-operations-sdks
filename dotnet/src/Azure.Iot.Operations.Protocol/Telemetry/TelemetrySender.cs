// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using Azure.Iot.Operations.Protocol.Models;
using System.Diagnostics;

namespace Azure.Iot.Operations.Protocol.Telemetry
{
    public abstract class TelemetrySender<T> : IAsyncDisposable
        where T : class
    {
        private readonly ApplicationContext _applicationContext;
        private readonly IMqttPubSubClient _mqttClient;
        private readonly IPayloadSerializer _serializer;
        private bool _isDisposed;
        private bool _hasBeenValidated;

        /// <summary>
        /// The timeout value that every telemetry message sent by this sender will use if no timeout is specified.
        /// </summary>
        /// <remarks>
        /// This value sets the message expiry interval field on the underlying MQTT message. This means
        /// that, if the message is successfully delivered to the MQTT broker, the message will be discarded
        /// by the broker if the broker has not managed to start onward delivery to a matching subscriber within
        /// this timeout.
        /// 
        /// If this value is equal to zero seconds, then the message will never expire at the broker.
        /// </remarks>
        private static readonly TimeSpan DefaultTelemetryTimeout = TimeSpan.FromSeconds(10);

        public string TopicPattern { get; init; }

        public string? TopicNamespace { get; set; }

        /// <summary>
        /// The topic token replacement map that this telemetry sender will use by default. Generally, this will include the token values
        /// for topic tokens such as "modelId" which should be the same for the duration of this command invoker's lifetime.
        /// </summary>
        /// <remarks>
        /// Tokens replacement values can also be specified per-telemetry by specifying the additionalTopicToken map in <see cref="SendTelemetryAsync(T, MqttQualityOfServiceLevel, TimeSpan?, CancellationToken)"/>.
        /// </remarks>
        public Dictionary<string, string> TopicTokenMap { get; protected set; }

        public TelemetrySender(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, IPayloadSerializer serializer)
        {
            _applicationContext = applicationContext;
            _mqttClient = mqttClient;
            _serializer = serializer;
            _hasBeenValidated = false;

            TopicPattern = AttributeRetriever.GetAttribute<TelemetryTopicAttribute>(this)?.Topic ?? string.Empty;
            TopicTokenMap = new();
        }

        /// <summary>
        /// Send telemetry with the default metadata.
        /// </summary>
        /// <param name="telemetry">The payload of the telemetry.</param>
        /// <param name="additionalTopicTokenMap">
        /// The topic token replacement map to use in addition to <see cref="TopicTokenMap"/>.If this map
        /// contains any keys that <see cref="TopicTokenMap"/> also has, then values specified in this map will take precedence.
        /// </param>
        /// <param name="qos">The quality of service to send the telemetry with.</param>
        /// <param name="telemetryTimeout">How long the telemetry message will be available on the broker for a receiver to receive.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendTelemetryAsync(T telemetry, Dictionary<string, string>? additionalTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            await SendTelemetryAsync(telemetry, new OutgoingTelemetryMetadata(), additionalTopicTokenMap, qos, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Send telemetry with custom metadata.
        /// </summary>
        /// <param name="telemetry">The payload of the telemetry.</param>
        /// <param name="metadata">The telemetry metadata.</param>
        /// <param name="additionalTopicTokenMap">
        /// The topic token replacement map to use in addition to <see cref="TopicTokenMap"/>.If this map
        /// contains any keys that <see cref="TopicTokenMap"/> also has, then values specified in this map will take precedence.
        /// </param>
        /// <param name="qos">The quality of service to send the telemetry with.</param>
        /// <param name="telemetryTimeout">How long the telemetry message will be available on the broker for a receiver to receive.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendTelemetryAsync(T telemetry, OutgoingTelemetryMetadata metadata, Dictionary<string, string>? additionalTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            ValidateAsNeeded(additionalTopicTokenMap);
            cancellationToken.ThrowIfCancellationRequested();

            string? clientId = _mqttClient.ClientId;
            if (string.IsNullOrEmpty(clientId))
            {
                throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before sending telemetry.");
            }

            TimeSpan verifiedMessageExpiryInterval = telemetryTimeout ?? DefaultTelemetryTimeout;

            if (verifiedMessageExpiryInterval <= TimeSpan.Zero)
            {
                throw AkriMqttException.GetConfigurationInvalidException("messageExpiryInterval", verifiedMessageExpiryInterval, "message expiry interval must have a positive value");
            }

            // Rounding up to the nearest second
            verifiedMessageExpiryInterval = TimeSpan.FromSeconds(Math.Ceiling(verifiedMessageExpiryInterval.TotalSeconds));

            if (verifiedMessageExpiryInterval.TotalSeconds > uint.MaxValue)
            {
                throw AkriMqttException.GetConfigurationInvalidException("messageExpiryInterval", verifiedMessageExpiryInterval, $"message expiry interval cannot be larger than {uint.MaxValue} seconds");
            }

            StringBuilder telemTopic = new();

            if (TopicNamespace != null)
            {
                if (!MqttTopicProcessor.IsValidReplacement(TopicNamespace))
                {
                    throw AkriMqttException.GetConfigurationInvalidException(nameof(TopicNamespace), TopicNamespace, "MQTT topic namespace is not valid");
                }

                telemTopic.Append(TopicNamespace);
                telemTopic.Append('/');
            }

            Dictionary<string, string> combinedTopicTokenMap = CombineTopicTokenMaps(TopicTokenMap, additionalTopicTokenMap);

            telemTopic.Append(MqttTopicProcessor.ResolveTopic(TopicPattern, combinedTopicTokenMap));

            try
            {
                SerializedPayloadContext serializedPayloadContext = _serializer.ToBytes(telemetry);
                MqttApplicationMessage applicationMessage = new(telemTopic.ToString(), qos)
                {
                    PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializedPayloadContext.PayloadFormatIndicator,
                    ContentType = serializedPayloadContext.ContentType,
                    MessageExpiryInterval = (uint)verifiedMessageExpiryInterval.TotalSeconds,
                    Payload = serializedPayloadContext.SerializedPayload,
                };

                if (metadata?.CloudEvent is not null)
                {
                    metadata.CloudEvent.Id ??= Guid.NewGuid().ToString();
                    metadata.CloudEvent.Time ??= DateTime.UtcNow;
                    metadata.CloudEvent.Subject ??= telemTopic.ToString();
                    metadata.CloudEvent.DataContentType = serializedPayloadContext.ContentType;
                }

                // Update HLC and use as the timestamp.
                await _applicationContext.ApplicationHlc.UpdateNowAsync(cancellationToken: cancellationToken);
                metadata!.Timestamp = new HybridLogicalClock(_applicationContext.ApplicationHlc);

                if (metadata != null)
                {
                    // The addition of the timestamp on to user properties happen below.
                    applicationMessage.AddMetadata(metadata);
                }

                applicationMessage.AddUserProperty(AkriSystemProperties.ProtocolVersion, $"{TelemetryVersion.MajorProtocolVersion}.{TelemetryVersion.MinorProtocolVersion}");
                applicationMessage.AddUserProperty(AkriSystemProperties.SourceId, clientId);

                MqttClientPublishResult pubAck = await _mqttClient.PublishAsync(applicationMessage, cancellationToken).ConfigureAwait(false);
                MqttClientPublishReasonCode pubReasonCode = pubAck.ReasonCode;
                if (pubReasonCode != MqttClientPublishReasonCode.Success)
                {
                    throw new AkriMqttException($"Telemetry sending to the topic '{telemTopic}' failed due to an unsuccessful publishing with the error code {pubReasonCode}")
                    {
                        Kind = AkriMqttErrorKind.MqttError,
                        IsShallow = false,
                        IsRemote = false,
                    };
                }
                Trace.TraceInformation($"Telemetry sent successfully to the topic '{telemTopic}'");
            }
            catch (SerializationException ex)
            {
                Trace.TraceError($"The message payload cannot be serialized due to error: {ex}");
                throw new AkriMqttException("The message payload cannot be serialized.", ex)
                {
                    Kind = AkriMqttErrorKind.PayloadInvalid,
                    IsShallow = true,
                    IsRemote = false,
                };
            }
            catch (Exception ex) when (ex is not AkriMqttException)
            {
                Trace.TraceError($"Sending telemetry failed due to a MQTT communication error: {ex}");
                throw new AkriMqttException($"Sending telemetry failed due to a MQTT communication error: {ex.Message}.", ex)
                {
                    Kind = AkriMqttErrorKind.Timeout,
                    IsShallow = false,
                    IsRemote = false,
                };
            }
        }

        private void ValidateAsNeeded(Dictionary<string, string>? additionalTopicTokenMap)
        {
            if (_hasBeenValidated)
            {
                return;
            }

            if (_mqttClient.ProtocolVersion != MqttProtocolVersion.V500)
            {
                throw AkriMqttException.GetConfigurationInvalidException(
                    "MQTTClient.ProtocolVersion",
                    _mqttClient.ProtocolVersion,
                    "The provided MQTT client is not configured for MQTT version 5");
            }

            Dictionary<string, string> combinedTopicTokenMap = CombineTopicTokenMaps(TopicTokenMap, additionalTopicTokenMap);

            PatternValidity patternValidity = MqttTopicProcessor.ValidateTopicPattern(TopicPattern, combinedTopicTokenMap, requireReplacement: true, out string errMsg, out string? errToken, out string? errReplacement);
            if (patternValidity != PatternValidity.Valid)
            {
                throw patternValidity switch
                {
                    PatternValidity.MissingReplacement => AkriMqttException.GetArgumentInvalidException(null, errToken!, null, errMsg),
                    PatternValidity.InvalidResidentReplacement => AkriMqttException.GetConfigurationInvalidException(errToken!, errReplacement!, errMsg),
                    _ => AkriMqttException.GetConfigurationInvalidException(nameof(TopicPattern), TopicPattern, errMsg),
                };
            }

            _hasBeenValidated = true;
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

        private static Dictionary<string, string> CombineTopicTokenMaps(Dictionary<string, string> baseMap, Dictionary<string, string>? additionalMap)
        {
            Dictionary<string, string> combinedTopicTokenMap = new(baseMap);

            additionalMap ??= new();
            foreach (string topicTokenKey in additionalMap.Keys)
            {
                combinedTopicTokenMap.TryAdd(topicTokenKey, additionalMap[topicTokenKey]);
            }

            return combinedTopicTokenMap;
        }

        protected virtual async ValueTask DisposeAsyncCore(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    await _mqttClient.DisposeAsync(disposing);
                }

                _isDisposed = true;
            }
        }
    }
}
