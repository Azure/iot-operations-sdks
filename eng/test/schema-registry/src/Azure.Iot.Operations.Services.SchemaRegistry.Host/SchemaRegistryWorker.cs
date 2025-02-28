using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;

namespace Azure.Iot.Operations.Services.SchemaRegistry.Host;

public class SchemaRegistryWorker(MqttSessionClient mqttClient, IServiceProvider provider, ILogger<SchemaRegistryWorker> logger, IConfiguration configuration)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Schema Registry Worker running at: {version}", typeof(MqttConnectionSettings).Assembly.FullName);
        await ConnectAsync(stoppingToken);
        SchemaRegistryService schemaRegistryService = provider.GetService<SchemaRegistryService>()!;
        await schemaRegistryService.StartAsync(null, stoppingToken);
    }

    private async Task ConnectAsync(CancellationToken stoppingToken)
    {
        string cs = configuration.GetConnectionString("Mq")! + $";ClientId={Environment.MachineName}";
        MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(cs);
        MqttClientConnectResult connAck = await mqttClient.ConnectAsync(mcs, stoppingToken);

        if (connAck.ResultCode != MqttClientConnectResultCode.Success)
        {
            logger.LogError("Failed to connect to MQTT broker: {connAck.ResultCode}", connAck.ResultCode);
            Environment.Exit(-1);
        }
        else
        {
            logger.LogInformation("Connected to {host} as {clientid} with persistent session {c}", mcs.HostName, mqttClient.ClientId, connAck.IsSessionPresent);
        }
    }
}
