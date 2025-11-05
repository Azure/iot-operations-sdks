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
            TopicPattern = topicPattern;
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
                if (payload.IsEmpty)
                {
                    return (Array.Empty<byte>() as T)!;
                }

                if (typeof(T) == typeof(byte[]))
                {
                    return (payload.ToArray() as T)!;
                }

                return null!;
            }

            public SerializedPayloadContext ToBytes<T>(T? payload)
                where T : class
            {
                if (payload is byte[] payload1)
                {
                    return new SerializedPayloadContext(new ReadOnlySequence<byte>(payload1), ContentType, PayloadFormatIndicator);
                }

                return new SerializedPayloadContext(ReadOnlySequence<byte>.Empty, ContentType, PayloadFormatIndicator);
            }
        }
    }
}
