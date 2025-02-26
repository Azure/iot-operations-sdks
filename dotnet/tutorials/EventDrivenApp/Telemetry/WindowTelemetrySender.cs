// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace EventDrivenApp;

[TelemetryTopic("sensor/window_data")]
public class WindowTelemetrySender : TelemetrySender<WindowData>
{
    internal WindowTelemetrySender(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
        : base(applicationContext, mqttClient, "WindowSender", new Utf8JsonSerializer())
    {
    }
}
