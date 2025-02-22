// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;
using EventDrivenApp.Models;

namespace EventDrivenApp.Telemetry;

[TelemetryTopic("sensor/data")]
public class SensorTelemetryReceiver : TelemetryReceiver<SensorData>
{
    internal SensorTelemetryReceiver(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
        : base(applicationContext, mqttClient, "SensorReceiver", new Utf8JsonSerializer())
    {
    }
}
