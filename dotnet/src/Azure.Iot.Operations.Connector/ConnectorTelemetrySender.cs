// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    }
}
