// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;

namespace SampleServer;

public class RpcHostBackgroundService(MqttSessionClient mqttClient, IServiceProvider provider, ILogger<RpcHostBackgroundService> logger, IConfiguration configuration) : BackgroundService
{
    CounterService? _counterService;
    GreeterService? _greetService;
    MathService? _mathService;
    MemMonService? _memMonService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _counterService = provider.GetService<CounterService>()!;
        _greetService = provider.GetService<GreeterService>()!;
        _mathService = provider.GetService<MathService>()!;
        _memMonService = provider.GetService<MemMonService>()!;

        MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(configuration.GetConnectionString("Default")!);
        MqttClientConnectResult connAck = await mqttClient.ConnectAsync(mcs, stoppingToken);
        logger.LogInformation("Connected to: {mcs} with session present: {s}", mcs, connAck.IsSessionPresent);

        await _counterService!.StartAsync(null, stoppingToken);
        await _greetService!.StartAsync(null, stoppingToken);
        await _mathService!.StartAsync(null, stoppingToken);
        await _memMonService!.StartAsync(null, stoppingToken);

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
        await _counterService!.DisposeAsync();
        await _greetService!.DisposeAsync();
        await _mathService!.DisposeAsync();
        await _memMonService!.DisposeAsync();
        await mqttClient.DisposeAsync();
    }
}
