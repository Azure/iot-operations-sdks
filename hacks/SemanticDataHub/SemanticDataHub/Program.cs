namespace SemanticDataHub
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Pipes;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Iot.Operations.Mqtt.Session;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Connection;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using SemanticDataServer.FirstModel;
    using Common;

    internal sealed class MyService : FirstModel.Service
    {
        public MyService(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : base(applicationContext, mqttClient)
        {
        }
    }

    internal class MappedSelection
    {
        public string DeviceAdress = string.Empty;
        public string CommFormat = string.Empty;
        public string DeviceTypeId = string.Empty;
    }

    internal class Program
    {
        const string clientId = "SemanticConsoleService";

        private volatile static MappedSelection mappedSelection = new MappedSelection();

        private static Dictionary<string, TelemetryBinding> GetTelemetryBindings()
        {
            Dictionary<string, TelemetryBinding> telemetryBindings = new ();

            TelemetryBinding bareBonesBinding = new TelemetryBinding("config/BareBonesDeviceBinding.json");
            telemetryBindings[bareBonesBinding.DeviceTypeId] = bareBonesBinding;

            TelemetryBinding deluxeBinding = new TelemetryBinding("config/DeluxeDeviceBinding.json");
            telemetryBindings[deluxeBinding.DeviceTypeId] = deluxeBinding;

            return telemetryBindings;
        }

        private static void SelectFromMapping(object sender, FileSystemEventArgs e)
        {
            while (true)
            {
                try
                {
                    using (StreamReader reader = File.OpenText(Constants.MappingFilePath))
                    {
                        using (JsonDocument mappingDoc = JsonDocument.Parse(reader.ReadToEnd()))
                        {
                            JsonElement mapElement = mappingDoc.RootElement.GetProperty(Constants.MqttNamespace);
                            MappedSelection newSelection = new MappedSelection
                            {
                                DeviceAdress = mapElement.GetProperty(Constants.MappingPropertyDeviceAddress).GetString() ?? string.Empty,
                                CommFormat = mapElement.GetProperty(Constants.MappingPropertyCommFormat).GetString() ?? string.Empty,
                                DeviceTypeId = mapElement.GetProperty(Constants.MappingPropertyDeviceTypeId).GetString() ?? string.Empty,
                            };

                            MappedSelection oldSelection = Interlocked.Exchange(ref mappedSelection, newSelection);
                            if (newSelection.DeviceAdress != oldSelection.DeviceAdress)
                            {
                                Console.WriteLine($"mapping data from address '{mappedSelection.DeviceAdress} with type ID {mappedSelection.DeviceTypeId}' expecting {mappedSelection.CommFormat} format");
                            }
                        }
                    }

                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                }
            }
        }

        private static string Jsonize(string data, string format)
        {
            switch (format)
            {
                case Constants.CommFormatJson:
                    return data;
                case Constants.CommFormatCsv:
                    return "[" + Regex.Replace(data, @"(?<=,|^)(?![.\d]+(?:,|$))[.\w]+(?=,|$)", (m) => $"\"{m.Groups[0].Value}\"" ) + "]";
                default:
                    throw new Exception($"Unrecognied commmunication format designator '{format}'");
            }
        }

        static async Task Main(string[] args)
        {
            ConversionTransformer.ConfigureModelUnits("SemanticDataHub/FirstModel.units.json");

            SelectFromMapping(null!, null!);

            Dictionary<string, TelemetryBinding> telemetryBindings = GetTelemetryBindings();

            using var watcher = new FileSystemWatcher(Path.GetDirectoryName(Constants.MappingFilePath) ?? ".");
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Changed += SelectFromMapping;
            watcher.Filter = Path.GetFileName(Constants.MappingFilePath);
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            ApplicationContext appContext = new();
            MqttSessionClient mqttSessionClient = new();

            Console.Write($"Connecting to MQTT broker as {clientId} ... ");
            await mqttSessionClient.ConnectAsync(new MqttConnectionSettings("localhost", clientId) { TcpPort = 1883, UseTls = false });
            Console.WriteLine("Connected!");

            MyService service = new(appContext, mqttSessionClient);
            service.ThermalConditionTelemetrySender.TopicNamespace = Constants.MqttNamespace;
            service.ArmPositionTelemetrySender.TopicNamespace = Constants.MqttNamespace;
            service.StatusTelemetrySender.TopicNamespace = Constants.MqttNamespace;
            service.ModeTelemetrySender.TopicNamespace = Constants.MqttNamespace;

            OutgoingTelemetryMetadata metadata = new();

            while (true)
            {
                MappedSelection currentSelection = mappedSelection;
                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", currentSelection.DeviceAdress, PipeDirection.In))
                {
                    Console.Write($"Attempting to connect to named pipe '{currentSelection.DeviceAdress}' ...");
                    await pipeClient.ConnectAsync();
                    Console.WriteLine(" Connected to pipe.");

                    TelemetryBinding telemetryBinding = telemetryBindings[currentSelection.DeviceTypeId];

                    using (StreamReader pipeReader = new StreamReader(pipeClient))
                    {
                        string? data;
                        while ((data = await pipeReader.ReadLineAsync()) != null && currentSelection.DeviceAdress == mappedSelection.DeviceAdress)
                        {
                            JToken telemetryToken = JToken.Parse(Jsonize(data, mappedSelection.CommFormat));

                            try
                            {
                                string? thermalConditionJson = telemetryBinding.GetTelemetry("ThermalCondition", telemetryToken);
                                if (thermalConditionJson != null)
                                {
                                    ThermalConditionTelemetry? thermalConditionTelemetry = JsonConvert.DeserializeObject<ThermalConditionTelemetry>(thermalConditionJson);
                                    if (thermalConditionTelemetry != null)
                                    {
                                        await service.SendTelemetryAsync(thermalConditionTelemetry, metadata);
                                    }
                                }

                                string? armPositionJson = telemetryBinding.GetTelemetry("ArmPosition", telemetryToken);
                                if (armPositionJson != null)
                                {
                                    ArmPositionTelemetry? armPositionTelemetry = JsonConvert.DeserializeObject<ArmPositionTelemetry>(armPositionJson);
                                    if (armPositionTelemetry != null)
                                    {
                                        await service.SendTelemetryAsync(armPositionTelemetry, metadata);
                                    }
                                }

                                string? statusJson = telemetryBinding.GetTelemetry("Status", telemetryToken);
                                if (statusJson != null)
                                {
                                    StatusTelemetry? statusTelemetry = JsonConvert.DeserializeObject<StatusTelemetry>(statusJson);
                                    if (statusTelemetry != null)
                                    {
                                        await service.SendTelemetryAsync(statusTelemetry, metadata);
                                    }
                                }

                                string? modeJson = telemetryBinding.GetTelemetry("Mode", telemetryToken);
                                if (modeJson != null)
                                {
                                    ModeTelemetry? modeTelemetry = JsonConvert.DeserializeObject<ModeTelemetry>(modeJson);
                                    if (modeTelemetry != null)
                                    {
                                        await service.SendTelemetryAsync(modeTelemetry, metadata);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Telemetry publication failed: {ex.Message}");
                                Environment.Exit(1);
                            }
                        }
                    }
                }
            }
        }
    }
}
