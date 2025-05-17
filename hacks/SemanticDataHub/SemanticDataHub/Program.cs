namespace SemanticDataHub
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.IO.Pipes;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Azure.Iot.Operations.Mqtt.Session;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Connection;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using SemanticDataServer.FirstModel;

    internal sealed class MyService : FirstModel.Service
    {
        public MyService(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : base(applicationContext, mqttClient)
        {
        }
    }

    internal class Program
    {
        const string clientId = "SemanticConsoleService";
        const string alphaPipeName = "DataSourceAlpha";

        private static readonly Regex alphaRegex = new Regex(@"^(\w+),([\d\.]+),(\w+)$", RegexOptions.Compiled);

        static async Task Main(string[] args)
        {
            string selection;
            using (JsonDocument mappingDoc = JsonDocument.Parse(File.OpenText("mapping.json").ReadToEnd()))
            {
                selection = mappingDoc.RootElement.GetProperty("TelemetryCollection").GetString()!;
            }

            Console.WriteLine($"mapping data from address '{selection}'");

            ApplicationContext appContext = new();
            MqttSessionClient mqttSessionClient = new();

            Console.Write($"Connecting to MQTT broker as {clientId} ... ");
            await mqttSessionClient.ConnectAsync(new MqttConnectionSettings("localhost", clientId) { TcpPort = 1883, UseTls = false });
            Console.WriteLine("Connected!");

            MyService service = new(appContext, mqttSessionClient);

            using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", alphaPipeName, PipeDirection.In))
            {
                Console.Write($"Attempting to connect to named pipe '{alphaPipeName}' ...");
                pipeClient.Connect();
                Console.WriteLine(" Connected to pipe.");

                using (StreamReader pipeReader = new StreamReader(pipeClient))
                {
                    string? data;
                    while ((data = pipeReader.ReadLine()) != null)
                    {
                        Match alphaMatch = alphaRegex.Match(data);

                        if (alphaMatch.Success)
                        {
                            string address = alphaMatch.Groups[1].Captures[0].Value;

                            if (address == selection)
                            {
                                TelemetryCollection snapshot = new TelemetryCollection
                                {
                                    SurfaceTemp = double.Parse(alphaMatch.Groups[2].Captures[0].Value),
                                    Mode = alphaMatch.Groups[3].Captures[0].Value,
                                };

                                OutgoingTelemetryMetadata metadata = new();

                                await service.SendTelemetryAsync(snapshot, metadata);
                            }
                        }
                    }
                }
            }
        }
    }
}
