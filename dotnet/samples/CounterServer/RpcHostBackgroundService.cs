using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;

namespace CounterServer;

public class RpcHostBackgroundService(MqttSessionClient mqttClient, CounterService counterService, ILogger<RpcHostBackgroundService> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(configuration.GetConnectionString("Default")!);
        MqttClientConnectResult connAck = await mqttClient.ConnectAsync(mcs, stoppingToken);
        logger.LogInformation("Connected to: {mcs} with session present: {s}", mcs, connAck.IsSessionPresent);
        await counterService.StartAsync(null, stoppingToken);
    }
    protected ValueTask DisposeAsync() => counterService!.DisposeAsync();
}
