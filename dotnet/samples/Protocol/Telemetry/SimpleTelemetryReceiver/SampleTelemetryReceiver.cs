// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace SimpleTelemetryReceiver
{
    [TelemetryTopic("some/telemetry/topic")]
    public class SampleTelemetryReceiver : TelemetryReceiver<PayloadObject>
    {
        public SampleTelemetryReceiver(
            ApplicationContext applicationContext,
            IMqttPubSubClient mqttClient,
            IPayloadSerializer serializer)
            : base(applicationContext, mqttClient, serializer)
        {
        }
    }
}
