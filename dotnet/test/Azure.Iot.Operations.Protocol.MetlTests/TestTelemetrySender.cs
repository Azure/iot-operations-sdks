// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;

    public class TestTelemetrySender : TelemetrySender<string>
    {
        internal TestTelemetrySender(IMqttPubSubClient mqttClient, string? telemetryName, IPayloadSerializer payloadSerializer)
            : base(mqttClient, telemetryName, payloadSerializer)
        {
        }
    }
}
