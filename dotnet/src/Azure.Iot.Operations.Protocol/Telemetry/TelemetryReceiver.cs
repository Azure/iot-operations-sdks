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
        private const int majorProtocolVersion = 1;
        private const int minorProtocolVersion = 0;

        private int[] supportedMajorProtocolVersions = [1];

        private static readonly int PreferredDispatchConcurrency = 10;
        private static readonly TimeSpan DefaultTelemetryTimeout = TimeSpan.FromSeconds(10);

        internal static IWallClock WallClock = new WallClock();

        private readonly IMqttPubSubClient mqttClient;
        private readonly string? telemetryName;
        private readonly IPayloadSerializer serializer;

        private Dispatcher? dispatcher;

        private bool isRunning;

        private string? topicNamespace;

        private bool isDisposed;

        public Func<string, T, IncomingTelemetryMetadata, Task>? OnTelemetryReceived { get; init; }

        public string ServiceGroupId { get; init; }

        public string ModelId { get; init; }

        public string TopicPattern { get; init; }

        public Dictionary<string, string>? CustomTopicTokenMap { get; init; }

        public string? TopicNamespace
        {
            get => topicNamespace;
            set
            {
                ObjectDisposedException.ThrowIf(isDisposed, this);
                if (value != null && !MqttTopicProcessor.IsValidReplacement(value))
                {
                    throw AkriMqttException.GetConfigurationInvalidException(nameof(TopicNamespace), value, "MQTT topic namespace is not valid");
                }

                topicNamespace = value;
            }
        }

        public TelemetryReceiver(IMqttPubSubClient mqttClient, string? telemetryName, IPayloadSerializer serializer)
        {
            this.mqttClient = mqttClient;
            this.telemetryName = telemetryName;
            this.serializer = serializer;

            isRunning = false;

            OnTelemetryReceived = default;

            dispatcher = null;

            ServiceGroupId = AttributeRetriever.GetAttribute<ServiceGroupIdAttribute>(this)?.Id ?? string.Empty;
            ModelId = AttributeRetriever.GetAttribute<ModelIdAttribute>(this)?.Id ?? string.Empty;
            TopicPattern = AttributeRetriever.GetAttribute<TelemetryTopicAttribute>(this)?.Topic ?? string.Empty;
            CustomTopicTokenMap = null;
            topicNamespace = null;

            mqttClient.ApplicationMessageReceivedAsync += MessageReceivedCallbackAsync;
        }

        private async Task MessageReceivedCallbackAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            string telemTopicFilter = GetTelemetryTopic();

            if (MqttTopicProcessor.DoesTopicMatchFilter(args.ApplicationMessage.Topic, telemTopicFilter))
            {
                string? requestProtocolVersion = args.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == AkriSystemProperties.ProtocolVersion)?.Value;

                if (!ProtocolVersion.TryParseProtocolVersion(requestProtocolVersion, out ProtocolVersion? protocolVersion))
                {
                    Trace.TraceError($"Telemetry with CorrelationId {args.ApplicationMessage.CorrelationData} provided a malformed protocol version {requestProtocolVersion}. The telemetry will be ignored by this receiver.");
                    return;
                }

                if (!supportedMajorProtocolVersions.Contains(protocolVersion!.MajorVersion))
                {
                    Trace.TraceError($"Telemetry with CorrelationId {args.ApplicationMessage.CorrelationData} requested an unsupported protocol version {requestProtocolVersion}. This telemetry reciever supports versions {ProtocolVersion.ToString(supportedMajorProtocolVersions)}. The telemetry will be ignored by this receiver.");
                    return;
                }

                args.AutoAcknowledge = false;

                DateTime messageReceivedTime = WallClock.UtcNow;

                TimeSpan telemetryTimeout = args.ApplicationMessage.MessageExpiryInterval != default ? TimeSpan.FromSeconds(args.ApplicationMessage.MessageExpiryInterval) : DefaultTelemetryTimeout;
                DateTime telemetryExpirationTime = messageReceivedTime + telemetryTimeout;

                MqttTopicProcessor.TryGetFieldValue(TopicPattern, args.ApplicationMessage.Topic, MqttTopicTokens.TelemetrySenderId, out string senderId);

                if ((args.ApplicationMessage.ContentType != null && args.ApplicationMessage.ContentType != this.serializer.ContentType) || OnTelemetryReceived == null)
                {
                    await GetDispatcher()(null, async () => { await args.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
                    return;
                }

                try
                {
                    var serializedPayload = this.serializer.FromBytes<T>(args.ApplicationMessage.PayloadSegment.Array);

                    var metadata = new IncomingTelemetryMetadata(args.ApplicationMessage, args.PacketIdentifier);

                    Func<Task> telemFunc = async () =>
                    {
                        try
                        {
                            await OnTelemetryReceived(senderId, serializedPayload, metadata);
                        }
                        catch (Exception innerEx)
                        {
                            Trace.TraceError($"Exception thrown while executing telemetry received callback: {innerEx.Message}");
                        }
                    };

                    await GetDispatcher()(telemFunc, async () => { await args.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
                }
                catch (Exception outerEx)
                {
                    Trace.TraceError($"Exception thrown while deserializing payload, callback skipped: {outerEx.Message}");
                }
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
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
                        "The provided MQTT client is not configured for MQTT version 5");
                }

                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before starting a telemetry receiver");
                }

                dispatcher ??= ExecutionDispatcher.CollectionInstance.GetDispatcher(clientId, PreferredDispatchConcurrency);

                string telemTopicFilter;
                try
                {
                    MqttTopicProcessor.ValidateTelemetryTopicPattern(TopicPattern, nameof(TopicPattern), telemetryName, ModelId, CustomTopicTokenMap);

                    telemTopicFilter = ServiceGroupId != string.Empty ? $"$share/{ServiceGroupId}/{GetTelemetryTopic()}" : GetTelemetryTopic();
                }
                catch (ArgumentException ex)
                {
                    throw AkriMqttException.GetConfigurationInvalidException(nameof(TopicPattern), TopicPattern, ex.Message, ex);
                }

                var topicFilter = new MqttTopicFilter(telemTopicFilter, MqttQualityOfServiceLevel.AtLeastOnce);

                MqttClientSubscribeOptions mqttSubscribeOptions = new MqttClientSubscribeOptions(topicFilter);

                MqttClientSubscribeResult subAck = await mqttClient.SubscribeAsync(mqttSubscribeOptions, cancellationToken).ConfigureAwait(false);
                subAck.ThrowIfNotSuccessSubAck(topicFilter.QualityOfServiceLevel);
                isRunning = true;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(isDisposed, this);

            if (isRunning)
            {
                string telemTopicFilter = ServiceGroupId != string.Empty ? $"$share/{ServiceGroupId}/{GetTelemetryTopic()}" : GetTelemetryTopic();

                MqttClientUnsubscribeOptions unsubscribeOptions = new MqttClientUnsubscribeOptions(telemTopicFilter);

                MqttClientUnsubscribeResult unsubAck = await mqttClient.UnsubscribeAsync(unsubscribeOptions, cancellationToken).ConfigureAwait(false);
                unsubAck.ThrowIfNotSuccessUnsubAck();
                isRunning = false;
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

        private string GetTelemetryTopic()
        {
            StringBuilder telemTopic = new();

            if (topicNamespace != null)
            {
                telemTopic.Append(topicNamespace);
                telemTopic.Append('/');
            }

            telemTopic.Append(MqttTopicProcessor.GetTelemetryTopic(TopicPattern, telemetryName: telemetryName, modelId: ModelId, customTokenMap: CustomTopicTokenMap));

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
            if (!isDisposed)
            {
                try
                {
                    await StopAsync();
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("Failed to stop the telemetry receiver while disposing it: {0}", ex);
                }

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