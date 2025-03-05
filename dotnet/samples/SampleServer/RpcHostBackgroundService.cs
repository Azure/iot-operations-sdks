// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;

namespace SampleServer;

public class RpcHostBackgroundService(MqttSessionClient mqttClient, IServiceProvider provider, ILogger<RpcHostBackgroundService> logger, IConfiguration configuration) : BackgroundService
{
    private CounterService? _counterService;
    private GreeterService? _greetService;
    private MathService? _mathService;
    private MemMonService? _memMonService;
    private CustomTopicTokenService? _customTopicTokenService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _counterService = provider.GetService<CounterService>()!;
        _greetService = provider.GetService<GreeterService>()!;
        _mathService = provider.GetService<MathService>()!;
        _memMonService = provider.GetService<MemMonService>()!;
        _customTopicTokenService = provider.GetService<CustomTopicTokenService>()!;

        MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(configuration.GetConnectionString("Default")!);
        MqttClientConnectResult connAck = await mqttClient.ConnectAsync(mcs, stoppingToken);
        logger.LogInformation("Connected to: {mcs} with session present: {s}", mcs, connAck.IsSessionPresent);

        await _counterService!.StartAsync(null, stoppingToken);
        await _greetService!.StartAsync(null, stoppingToken);
        await _mathService!.StartAsync(null, stoppingToken);
        await _memMonService!.StartAsync(null, stoppingToken);
        await _customTopicTokenService.StartAsync(null, stoppingToken);

        _ = Task.Run(async () =>
        {
            // Periodically send telemetry from custom topic token service to the custom topic token client
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    Dictionary<string, string> transientTopicTokens = new()
                    {
                        ["myCustomTopicToken"] = Guid.NewGuid().ToString()
                    };
                    await _customTopicTokenService.SendTelemetryAsync(new(), new(), transientTopicTokens);
                }
                catch (Exception)
                {
                    // Likely no matching subscribers. Safe to ignore.
                }
            }
        }, stoppingToken);

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
