// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;
using TestEnvoys.dtmi_com_example_CustomTopicTokens__1;

namespace SampleClient;

internal class CustomTopicTokenClient : CustomTopicTokens.Client
{
    public CustomTopicTokenClient(ApplicationContext applicationContext, MqttSessionClient mqttClient) : base(applicationContext, mqttClient)
    {
    }

    public override Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
    {
        // Log or process telemetry data
        Console.WriteLine($"Telemetry received from {senderId}: Cutom topic token value={telemetry.AnnouncedCustomTopicTokenValue}");
        return Task.CompletedTask;
    }
}
