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
    using Common;

    internal sealed class MyClient : FirstModel.Client
    {
        public MyClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : base(applicationContext, mqttClient)
        {
        }

        public override Task ReceiveTelemetry(string senderId, ThermalConditionTelemetry telemetry, IncomingTelemetryMetadata metadata)
        {
            Console.WriteLine($"ThermalCondition:  InternalTemp = {telemetry.ThermalCondition.InternalTemp:F1} degrees Celsius, ExternalTemp = {telemetry.ThermalCondition.ExternalTemp:F1} degrees Celsius");
            return Task.CompletedTask;
        }

        public override Task ReceiveTelemetry(string senderId, ArmPositionTelemetry telemetry, IncomingTelemetryMetadata metadata)
        {
            Console.WriteLine($"ArmPosition:  ({telemetry.ArmPosition.X:F1}, {telemetry.ArmPosition.Y:F1})");
            return Task.CompletedTask;
        }

        public override Task ReceiveTelemetry(string senderId, StatusTelemetry telemetry, IncomingTelemetryMetadata metadata)
        {
            Console.WriteLine($"Status:  {telemetry.Status.ToString()}");
            return Task.CompletedTask;
        }

        public override Task ReceiveTelemetry(string senderId, ModeTelemetry telemetry, IncomingTelemetryMetadata metadata)
        {
            Console.WriteLine($"Mode:  {telemetry.Mode}");
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

            Console.Write($"Connecting to MQTT broker as {clientId} ... ");
            await mqttSessionClient.ConnectAsync(new MqttConnectionSettings("localhost", clientId) { TcpPort = 1883, UseTls = false });
            Console.WriteLine("Connected!");

            MyClient client = new(appContext, mqttSessionClient);
            client.ThermalConditionTelemetryReceiver.TopicNamespace = Constants.MqttNamespace;
            client.ArmPositionTelemetryReceiver.TopicNamespace = Constants.MqttNamespace;
            client.StatusTelemetryReceiver.TopicNamespace = Constants.MqttNamespace;
            client.ModeTelemetryReceiver.TopicNamespace = Constants.MqttNamespace;

            await client.StartAsync();

            await Task.Delay(runDuration);

            await client.StopAsync();
        }
    }
}
