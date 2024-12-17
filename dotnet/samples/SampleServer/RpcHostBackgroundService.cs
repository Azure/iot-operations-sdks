using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;

namespace SampleServer;

public class RpcHostBackgroundService(MqttSessionClient mqttClient, IServiceProvider provider, ILogger<RpcHostBackgroundService> logger, IConfiguration configuration) : BackgroundService
{
    CounterService? counterService;
    GreeterService? greetService;
    MathService? mathService;
    MemMonService? memMonService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        counterService = provider.GetService<CounterService>()!;
        greetService = provider.GetService<GreeterService>()!;
        mathService = provider.GetService<MathService>()!;
        memMonService = provider.GetService<MemMonService>()!;

        MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(configuration.GetConnectionString("Default")!);
        MqttClientConnectResult connAck = await mqttClient.ConnectAsync(mcs, stoppingToken);
        logger.LogInformation("Connected to: {mcs} with session present: {s}", mcs, connAck.IsSessionPresent);

        await counterService!.StartAsync(null, stoppingToken);
        await greetService!.StartAsync(null, stoppingToken);
        await mathService!.StartAsync(null, stoppingToken);
        await memMonService!.StartAsync(null, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            string? res = Console.ReadLine();
            if (int.TryParse(res, out int delay))
            {
                await Console.Out.WriteLineAsync($"Disconnecting with delay {delay} s.");
                await mqttClient.DisconnectAsync(
                    new MqttClientDisconnectOptions()
                    { 
                        Reason = MqttClientDisconnectOptionsReason.AdministrativeAction,
                        ReasonString = "force reconnect",
                    }, 
                    stoppingToken);

                await Task.Delay(delay * 1000);
                MqttClientConnectResult connAck2 = await mqttClient.ConnectAsync(mcs, stoppingToken);
                logger.LogInformation("Connected to: {mcs} with session present: {s}", mcs, connAck2.IsSessionPresent);
            }
        }
    }

    protected async ValueTask DisposeAsync()
    {
        await counterService!.DisposeAsync();
        await greetService!.DisposeAsync();
        await mathService!.DisposeAsync();
        await memMonService!.DisposeAsync();
        await mqttClient.DisposeAsync();
    }
}
