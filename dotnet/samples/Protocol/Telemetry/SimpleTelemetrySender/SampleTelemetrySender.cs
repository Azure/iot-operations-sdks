// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace SimpleTelemetrySender
{
    [TelemetryTopic("some/telemetry/topic")]
    public class SampleTelemetrySender : TelemetrySender<PayloadObject>
    {
        public SampleTelemetrySender(
            ApplicationContext applicationContext,
            IMqttPubSubClient mqttClient,
            IPayloadSerializer serializer)
            : base(applicationContext, mqttClient, serializer)
        {
        }
    }
}
