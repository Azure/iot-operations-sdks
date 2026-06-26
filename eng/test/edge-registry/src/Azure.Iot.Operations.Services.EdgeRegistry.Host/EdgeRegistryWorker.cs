using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;

namespace Azure.Iot.Operations.Services.EdgeRegistry.Host;

public class EdgeRegistryWorker(MqttSessionClient mqttClient, IServiceProvider provider, ILogger<EdgeRegistryWorker> logger, IConfiguration configuration)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Edge Registry Worker running at: {version}", typeof(MqttConnectionSettings).Assembly.FullName);
        await ConnectAsync(stoppingToken);
        EdgeRegistryService edgeRegistryService = provider.GetService<EdgeRegistryService>()!;
        await edgeRegistryService.StartAsync(null, cancellationToken: stoppingToken);
        logger.LogInformation("Edge Registry service is now accepting commands");
    }

    private async Task ConnectAsync(CancellationToken stoppingToken)
    {
        string cs = configuration.GetConnectionString("Mq")!;
        MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(cs);
        MqttClientConnectResult connAck = await mqttClient.ConnectAsync(mcs, stoppingToken);

        if (connAck.ResultCode != MqttClientConnectResultCode.Success)
        {
            logger.LogError("Failed to connect to MQTT broker: {code}", connAck.ResultCode);
            Environment.Exit(-1);
        }
        else
        {
            logger.LogInformation("Connected to {host} as {clientid} with persistent session {c}", mcs.HostName, mqttClient.ClientId, connAck.IsSessionPresent);
        }
    }
}
