using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Mqtt.Session;

namespace EventDrivenApp;

public class SubscribeService : BackgroundService
{
    private readonly ILogger<SubscribeService> _logger;
    private MqttSessionClient _mqttClient;

    public SubscribeService(MqttSessionClient mqttClient, ILogger<SubscribeService> logger)
    {
        _logger = logger;
        _mqttClient = mqttClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            MqttConnectionSettings mcs = MqttConnectionSettings.FromEnvVars();

            await _mqttClient.ConnectAsync(mcs, stoppingToken);
            await Console.Out.WriteLineAsync($"Connected to: {mcs}");
//            var server_id = configuration.GetValue<string>("COUNTER_SERVER_ID") ?? "CounterServer";
            await RunWindow();
            await _mqttClient.DisconnectAsync();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            Environment.Exit(1);
        }

        async Task RunWindow()
        {
            // Sleep for 10 seconds
            await Task.Delay(10000);
        }

        // while (!stoppingToken.IsCancellationRequested)
        // {
        //     if (_logger.IsEnabled(LogLevel.Information))
        //     {
        //         _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
        //     }
        //     await Task.Delay(1000, stoppingToken);
        // }
    }
}
