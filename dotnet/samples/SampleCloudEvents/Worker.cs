// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Telemetry;
using SchemaFormat = Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_Format__1;
using SchemaType = Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_SchemaType__1;

namespace SampleCloudEvents;

public class Worker(MqttSessionClient mqttClient, OvenService ovenService, SchemaRegistryClient srClient, ILogger<Worker> logger, IConfiguration configuration) : BackgroundService
{
    DateTime started = DateTime.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectAsync(stoppingToken);

        var schemaInfo = await srClient.PutAsync(File.ReadAllText("OvenTelemetry.schema.json"), SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema);

        

        OutgoingTelemetryMetadata metadata = new()
        {
            CloudEvent = new CloudEvent(new Uri("aio://oven/sample"))
            {
                DataSchema = $"sr://{schemaInfo?.Namespace}/{schemaInfo?.Name}#{schemaInfo?.Version}"
            }
        };

        logger.LogInformation(metadata.CloudEvent.DataSchema);

        int counter = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            await ovenService.SendTelemetryAsync(new dtmi_akri_samples_oven__1.TelemetryCollection()
            {
                ExternalTemperature = 100 - counter,
                InternalTemperature = 200 + counter,
            }, metadata, MqttQualityOfServiceLevel.AtMostOnce);
            
            if (counter % 2 == 0)
            {
                await ovenService.SendTelemetryAsync(new dtmi_akri_samples_oven__1.TelemetryCollection()
                {
                    OperationSummary = new()
                    {
                        NumberOfCakes = counter,
                        StartingTime = started,
                        TotalDuration = DateTime.UtcNow - started
                    }
                }, metadata, MqttQualityOfServiceLevel.AtMostOnce);
            }
            logger.LogInformation("messages sent {counter}", counter++);
            await Task.Delay(1000, stoppingToken);
        }
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
