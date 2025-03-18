// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Telemetry;
using CustomComm;

namespace Server
{
    internal class AvroService : AvroComm.AvroModel.AvroModel.Service
    {
        public AvroService(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : base(applicationContext, mqttClient)
        {
        }
    }

    internal class JsonService : JsonComm.JsonModel.JsonModel.Service
    {
        public JsonService(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : base(applicationContext, mqttClient)
        {
        }
    }

    internal class RawService : RawComm.RawModel.RawModel.Service
    {
        public RawService(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : base(applicationContext, mqttClient)
        {
        }
    }

    internal class CustomService : CustomComm.CustomModel.CustomModel.Service
    {
        public CustomService(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : base(applicationContext, mqttClient)
        {
        }
    }

    internal sealed class Program
    {
        private enum CommFormat
        {
            Avro,
            Json,
            Raw,
            Custom
        }

        const string avroServerId = "AvroDotnetServer";
        const string jsonServerId = "JsonDotnetServer";
        const string rawServerId = "RawDotnetServer";
        const string customServerId = "CustomDotnetServer";

        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: Server {AVRO|JSON|RAW|CUSTOM} iterations [interval_in_seconds]");
                return;
            }

            (CommFormat format, string serverId) = args[0].ToLowerInvariant() switch
            {
                "avro" => (CommFormat.Avro, avroServerId),
                "json" => (CommFormat.Json, jsonServerId),
                "raw" => (CommFormat.Raw, rawServerId),
                "custom" => (CommFormat.Custom, customServerId),
                _ => throw new ArgumentException("format must be AVRO or JSON or RAW or CUSTOM", nameof(args))
            };

            int iterations = int.Parse(args[1], CultureInfo.InvariantCulture);

            TimeSpan interval = TimeSpan.FromSeconds(args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 1);

            ApplicationContext appContext = new();
            MqttSessionClient mqttSessionClient = new();

            Console.Write($"Connecting to MQTT broker as {serverId} ... ");
            await mqttSessionClient.ConnectAsync(new MqttConnectionSettings("localhost") { TcpPort = 1883, UseTls = false, ClientId = serverId });
            Console.WriteLine("Connected!");

            Console.WriteLine("Starting send loop");
            Console.WriteLine();

            switch (format)
            {
                case CommFormat.Avro:
                    await SendAvro(appContext, mqttSessionClient, iterations, interval);
                    break;
                case CommFormat.Json:
                    await SendJson(appContext, mqttSessionClient, iterations, interval);
                    break;
                case CommFormat.Raw:
                    await SendRaw(appContext, mqttSessionClient, iterations, interval);
                    break;
                case CommFormat.Custom:
                    await SendCustom(appContext, mqttSessionClient, iterations, interval);
                    break;
            }

            Console.WriteLine();
            Console.WriteLine("Stopping send loop");
        }

        private static async Task SendAvro(ApplicationContext appContext, MqttSessionClient mqttSessionClient, int iterations, TimeSpan interval)
        {
            AvroService service = new(appContext, mqttSessionClient);

            for (int i = 0; i < iterations; i++)
            {
                Console.WriteLine($"  Sending iteration {i}");
                await service.SendTelemetryAsync(
                    new AvroComm.AvroModel.TelemetryCollection
                    {
                        Lengths = new List<double>() { i, i + 1, i + 2 },
                        Proximity = i % 3 == 0 ?
                            AvroComm.AvroModel.ProximitySchema.far :
                            AvroComm.AvroModel.ProximitySchema.near,
                        Schedule = new AvroComm.AvroModel.ScheduleSchema
                        {
                            Course = "Math",
                            Credit = new TimeSpan(i + 2, i + 1, i).ToString(),
                        },
                        Data = Encoding.UTF8.GetBytes($"Sample data {i}")
                    },
                    new OutgoingTelemetryMetadata(),
                    new Dictionary<string, string> { { "myToken", "DotnetReplacement" } });

                await Task.Delay(interval);
            }
        }

        private static async Task SendJson(ApplicationContext appContext, MqttSessionClient mqttSessionClient, int iterations, TimeSpan interval)
        {
            JsonService service = new(appContext, mqttSessionClient);

            for (int i = 0; i < iterations; i++)
            {
                Console.WriteLine($"  Sending iteration {i}");
                await service.SendTelemetryAsync(
                    new JsonComm.JsonModel.TelemetryCollection
                    {
                        Lengths = new() { i, i + 1, i + 2 },
                        Proximity = i % 3 == 0 ?
                            JsonComm.JsonModel.ProximitySchema.Far :
                            JsonComm.JsonModel.ProximitySchema.Near,
                        Schedule = new JsonComm.JsonModel.ScheduleSchema
                        {
                            Course = "Math",
                            Credit = new TimeSpan(i + 2, i + 1, i),
                        },
                        Data = Encoding.UTF8.GetBytes($"Sample data {i}")
                    },
                    new OutgoingTelemetryMetadata(),
                    new Dictionary<string, string> { { "myToken", "DotnetReplacement" } });

                await Task.Delay(interval);
            }
        }

        private static async Task SendRaw(ApplicationContext appContext, MqttSessionClient mqttSessionClient, int iterations, TimeSpan interval)
        {
            RawService service = new(appContext, mqttSessionClient);

            for (int i = 0; i < iterations; i++)
            {
                Console.WriteLine($"  Sending iteration {i}");
                byte[] payload = Encoding.UTF8.GetBytes($"Sample data {i}");
                await service.SendTelemetryAsync(
                    payload,
                    new OutgoingTelemetryMetadata(),
                    new Dictionary<string, string> { { "myToken", "DotnetReplacement" } });

                await Task.Delay(interval);
            }
        }

        private static async Task SendCustom(ApplicationContext appContext, MqttSessionClient mqttSessionClient, int iterations, TimeSpan interval)
        {
            CustomService service = new(appContext, mqttSessionClient);

            for (int i = 0; i < iterations; i++)
            {
                Console.WriteLine($"  Sending iteration {i}");
                var payload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes($"Sample data {i}"));
                await service.SendTelemetryAsync(
                    new CustomPayload(payload, "text/csv", MqttPayloadFormatIndicator.CharacterData),
                    new OutgoingTelemetryMetadata(),
                    new Dictionary<string, string> { { "myToken", "DotnetReplacement" } });

                await Task.Delay(interval);
            }
        }
    }
}
