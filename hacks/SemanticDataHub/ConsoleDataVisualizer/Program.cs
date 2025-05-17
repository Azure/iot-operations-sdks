namespace ConsoleDataVisualizer
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using Azure.Iot.Operations.Mqtt.Session;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Connection;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using SemanticDataClient.FirstModel;

    internal sealed class MyClient : FirstModel.Client
    {
        public MyClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : base(applicationContext, mqttClient)
        {
        }

        public override Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
        {
            Console.WriteLine($"SurfaceTemp = {telemetry.SurfaceTemp} degrees Celsius, Mode = {telemetry.Mode}");
            return Task.CompletedTask;
        }
    }

    internal class Program
    {
        const string clientId = "SemanticConsoleClient";

        static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: ConsoleDataVisualizer <SECONDS_TO_RUN>");
                return;
            }

            TimeSpan runDuration = TimeSpan.FromSeconds(int.Parse(args[0], CultureInfo.InvariantCulture));

            ApplicationContext appContext = new();
            MqttSessionClient mqttSessionClient = new();

            Console.Write($"Connecting to MQTT broker as {clientId} ..0. ");
            await mqttSessionClient.ConnectAsync(new MqttConnectionSettings("localhost", clientId) { TcpPort = 1883, UseTls = false });
            Console.WriteLine("Connected!");

            MyClient client = new(appContext, mqttSessionClient);

            await client.StartAsync();

            await Task.Delay(runDuration);

            await client.StopAsync();
        }
    }
}
