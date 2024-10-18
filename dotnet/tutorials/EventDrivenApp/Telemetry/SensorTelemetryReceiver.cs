// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace EventDrivenApp;

[TelemetryTopic("sensor/data")]
public class SensorTelemetryReceiver : TelemetryReceiver<SensorData>
{
    internal SensorTelemetryReceiver(IMqttPubSubClient mqttClient)
        : base(mqttClient, "SensorReceiver", new Utf8JsonSerializer())
    {
    }
}
