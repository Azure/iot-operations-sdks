// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;

namespace SampleReadCloudEvents;

public class Worker(MqttSessionClient mqttClient, OvenClient ovenClient, ILogger<Worker> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectAsync(stoppingToken);
        await ovenClient.StartAsync(stoppingToken);
    }

    private async Task ConnectAsync(CancellationToken stoppingToken)
    {
        MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(configuration.GetConnectionString("Default")!);
        MqttClientConnectResult connAck = await mqttClient.ConnectAsync(mcs, stoppingToken);
        
        if (connAck.ResultCode != MqttClientConnectResultCode.Success)
        {
            logger.LogError("Failed to connect to MQTT broker: {connAck.ResultCode}", connAck.ResultCode);
            return;
        }
        else
        {
            logger.LogInformation("Connected with persistent session {c}", connAck.IsSessionPresent);
        }
    }
}
