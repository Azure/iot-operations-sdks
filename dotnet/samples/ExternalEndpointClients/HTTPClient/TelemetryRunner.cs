using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HTTPClient;

public class StringTelemetrySender : TelemetrySender<string>
    {
        public StringTelemetrySender(IMqttPubSubClient mqttClient)
            : base(mqttClient, "test", new Utf8JsonSerializer())
        {
        }
    }

    public class StringTelemetryReceiver : TelemetryReceiver<string>
    {
        public StringTelemetryReceiver(IMqttPubSubClient mqttClient)
            : base(mqttClient, "test", new Utf8JsonSerializer())
        {
        }
    }

public class TelemetryRunner(MqttSessionClient mqttClient, IServiceProvider serviceProvider, ILogger<TelemetryRunner> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int telemetryCount = 0;
        string telemetryReceived = string.Empty;
        MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(configuration!.GetConnectionString("Default")! + ";ClientId=HTTPClient-" + Environment.TickCount);

        await mqttClient.ConnectAsync(mcs, stoppingToken);
        await Console.Out.WriteLineAsync($"Connected to: {mcs}");

        var receiver = new StringTelemetryReceiver(mqttClient)
            {
                TopicPattern = "tutorial",
                OnTelemetryReceived = (string _, string response, IncomingTelemetryMetadata data) =>
                {
                    telemetryReceived = response;
                    telemetryCount++;
                    return Task.CompletedTask;
                },
            };

            var sender = new StringTelemetrySender(mqttClient)
            {
                TopicPattern = "tutorial",
                ModelId = "someModel",
            };

            await receiver.StartAsync();
            string telemetry = "someTelemetry";
            for (int i = 0; i < 5; i++)
            {
                await sender.SendTelemetryAsync(telemetry);
            }

            await Task.Delay(5000);

            await receiver.StopAsync();
        

        await mqttClient.DisconnectAsync();
        await mqttClient.DisposeAsync();
    }
}