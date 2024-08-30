using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.Telemetry;

public abstract class TelemetrySender<T> : IAsyncDisposable
    where T : class
{

    private const int majorProtocolVersion = 1;
    private const int minorProtocolVersion = 0;

    private readonly IMqttPubSubClient _mqttClient;
    private readonly string? _telemetryName;
    private readonly IPayloadSerializer _serializer;
    private bool _isDisposed;
    private bool _hasBeenValidated;
    private string? _topicNamespace;


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

    public string ModelId { get; init; }

    public string TopicPattern { get; init; }

    public Dictionary<string, string>? CustomTopicTokenMap { get; init; }

    public string? TopicNamespace
    {
        get => _topicNamespace;
        set
        {
            if (value != null && !MqttTopicProcessor.IsValidReplacement(value))
            {
                throw AkriMqttException.GetConfigurationInvalidException(nameof(TopicNamespace), value, "MQTT topic namespace is not valid");
            }

            _topicNamespace = value;
        }
    }

    public TelemetrySender(IMqttPubSubClient mqttClient, string? telemetryName, IPayloadSerializer serializer)
    {
        _mqttClient = mqttClient;
        _telemetryName = telemetryName;
        _serializer = serializer;
        _hasBeenValidated = false;
        _topicNamespace = null;

        ModelId = AttributeRetriever.GetAttribute<ModelIdAttribute>(this)?.Id ?? string.Empty;
        TopicPattern = AttributeRetriever.GetAttribute<TelemetryTopicAttribute>(this)?.Topic ?? string.Empty;
        CustomTopicTokenMap = null;
    }

    public async Task SendTelemetryAsync(T telemetry, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
    {
        await SendTelemetryAsync(telemetry, new OutgoingTelemetryMetadata(), qos, telemetryTimeout, cancellationToken);
    }

    public async Task SendTelemetryAsync(T telemetry, OutgoingTelemetryMetadata metadata, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? messageExpiryInterval = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ValidateAsNeeded();
        cancellationToken.ThrowIfCancellationRequested();

        TimeSpan verifiedMessageExpiryInterval = messageExpiryInterval ?? DefaultTelemetryTimeout;

        if (verifiedMessageExpiryInterval <= TimeSpan.Zero)
        {
            throw AkriMqttException.GetConfigurationInvalidException("messageExpiryInterval", verifiedMessageExpiryInterval, "message expiry interval must have a positive value");
        }

        if (verifiedMessageExpiryInterval.TotalSeconds > uint.MaxValue)
        {
            throw AkriMqttException.GetConfigurationInvalidException("messageExpiryInterval", verifiedMessageExpiryInterval, $"message expiry interval cannot be larger than {uint.MaxValue} seconds");
        }

        StringBuilder telemTopic = new();

        if (_topicNamespace != null)
        {
            telemTopic.Append(_topicNamespace);
            telemTopic.Append('/');
        }

        try
        {
            telemTopic.Append(MqttTopicProcessor.GetTelemetryTopic(TopicPattern, _telemetryName, _mqttClient.ClientId, ModelId, CustomTopicTokenMap));

            if (metadata?.CloudEvent is not null)
            {
                metadata.CloudEvent.Id = Guid.NewGuid().ToString();
                metadata.CloudEvent.Time = DateTime.UtcNow;
                metadata.CloudEvent.Subject = telemTopic.ToString();
                metadata.CloudEvent.DataContentType = _serializer.ContentType;
                
                // TBD https://github.com/microsoft/mqtt-patterns/discussions/917
                // metadata.CloudEventsMetadata.DataSchema = _serializer.Schema; 
            }

            var applicationMessage = new MqttApplicationMessage(telemTopic.ToString(), qos)
            {
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)_serializer.CharacterDataFormatIndicator,
                ContentType = _serializer.ContentType,
                MessageExpiryInterval = (uint)verifiedMessageExpiryInterval.TotalSeconds,
                PayloadSegment = _serializer.ToBytes(telemetry) ?? Array.Empty<byte>(),
            };

            if (metadata != null)
            { 
                applicationMessage.AddMetadata(metadata);
            }

            applicationMessage.AddUserProperty(AkriSystemProperties.ProtocolVersion, $"{majorProtocolVersion}.{minorProtocolVersion}");

            MqttClientPublishResult pubAck = await _mqttClient.PublishAsync(applicationMessage, cancellationToken).ConfigureAwait(false);
            var pubReasonCode = pubAck.ReasonCode;
            if (pubReasonCode != MqttClientPublishReasonCode.Success)
            {
                throw new AkriMqttException($"Telemetry sending to the topic '{telemTopic}' failed due to an unsuccessful publishing with the error code {pubReasonCode}")
                {
                    Kind = AkriMqttErrorKind.MqttError,
                    InApplication = false,
                    IsShallow = false,
                    IsRemote = false,
                };
            }
        }
        catch (ArgumentException ex)
        {
            throw AkriMqttException.GetConfigurationInvalidException(nameof(TopicPattern), TopicPattern, ex.Message, ex);
        }
        catch (SerializationException ex)
        {
            throw new AkriMqttException("The message payload cannot be serialized.", ex)
            {
                Kind = AkriMqttErrorKind.PayloadInvalid,
                InApplication = false,
                IsShallow = true,
                IsRemote = false,
            };
        }
        catch (Exception ex) when (ex is not AkriMqttException)
        {
            throw new AkriMqttException($"Sending telemetry failed due to a MQTT communication error: {ex.Message}.", ex)
            {
                Kind = AkriMqttErrorKind.Timeout,
                InApplication = false,
                IsShallow = false,
                IsRemote = false,
            };
        }
    }

    private void ValidateAsNeeded()
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

        try
        {
            MqttTopicProcessor.ValidateTelemetryTopicPattern(TopicPattern, nameof(TopicPattern), _telemetryName, ModelId, CustomTopicTokenMap);
        }
        catch (ArgumentException ex)
        {
            throw AkriMqttException.GetConfigurationInvalidException(nameof(TopicPattern), TopicPattern, ex.Message, ex);
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
