// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A telemetry sender that accepts pre-serialized byte arrays and supports dynamic topic patterns.
    /// This is used by the connector worker to publish telemetry with all the benefits of TelemetrySender
    /// (cloud events, protocol versioning, HLC timestamps, etc.) while allowing flexible topic configuration.
    /// </summary>
    internal class ConnectorTelemetrySender : TelemetrySender<byte[]>
    {
        public ConnectorTelemetrySender(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string topicPattern)
            : base(applicationContext, mqttClient, new PassthroughSerializer())
        {
            // Override the topic pattern with the one provided
            TopicPattern = topicPattern;
        }

        /// <summary>
        /// Override DisposeAsync to prevent disposal of the shared MQTT client.
        /// The MQTT client is owned by the ConnectorWorker and should not be disposed by individual telemetry senders.
        /// </summary>
        protected override ValueTask DisposeAsyncCore(bool disposing)
        {
            // Do not dispose the MQTT client as it's shared across multiple telemetry sends
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// A passthrough serializer that handles byte arrays without any conversion.
        /// </summary>
        private class PassthroughSerializer : IPayloadSerializer
        {
            private const string ContentType = "application/octet-stream";
            private const Protocol.Models.MqttPayloadFormatIndicator PayloadFormatIndicator = Protocol.Models.MqttPayloadFormatIndicator.Unspecified;

            public T FromBytes<T>(ReadOnlySequence<byte> payload, string? contentType, Protocol.Models.MqttPayloadFormatIndicator payloadFormatIndicator)
                where T : class
            {
                if (typeof(T) != typeof(byte[]))
                {
                    throw new NotSupportedException($"PassthroughSerializer only supports byte[] payloads, but was asked to deserialize to {typeof(T).Name}");
                }

                return (payload.IsEmpty ? Array.Empty<byte>() : payload.ToArray()) as T!;
            }

            public SerializedPayloadContext ToBytes<T>(T? payload)
                where T : class
            {
                if (typeof(T) != typeof(byte[]))
                {
                    throw new NotSupportedException($"PassthroughSerializer only supports byte[] payloads, but was asked to serialize {typeof(T).Name}");
                }

                var bytes = payload as byte[];
                if (bytes == null)
                {
                    return new(ReadOnlySequence<byte>.Empty, ContentType, PayloadFormatIndicator);
                }
                return new(new(bytes), ContentType, PayloadFormatIndicator);
            }
        }
    }
}
