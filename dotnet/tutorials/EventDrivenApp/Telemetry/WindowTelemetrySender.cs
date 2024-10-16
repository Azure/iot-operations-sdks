// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace EventDrivenApp;

[TelemetryTopic("sensor/windowdata")]
public class WindowTelemetrySender : TelemetrySender<WindowData>
{
    internal WindowTelemetrySender(IMqttPubSubClient mqttClient)
        : base(mqttClient, "WindowSender", new Utf8JsonSerializer())
    {
    }
}
