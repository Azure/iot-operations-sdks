// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using CustomComm;

namespace Server
{
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

            MqttSessionClient mqttSessionClient = new();

            Console.Write($"Connecting to MQTT broker as {serverId} ... ");
            await mqttSessionClient.ConnectAsync(new MqttConnectionSettings("localhost") { TcpPort = 1883, UseTls = false, ClientId = serverId });
            Console.WriteLine("Connected!");

            Console.WriteLine("Starting send loop");
            Console.WriteLine();

            switch (format)
            {
                case CommFormat.Avro:
                    await SendAvro(mqttSessionClient, iterations, interval);
                    break;
                case CommFormat.Json:
                    await SendJson(mqttSessionClient, iterations, interval);
                    break;
                case CommFormat.Raw:
                    await SendRaw(mqttSessionClient, iterations, interval);
                    break;
                case CommFormat.Custom:
                    await SendCustom(mqttSessionClient, iterations, interval);
                    break;
            }

            Console.WriteLine();
            Console.WriteLine("Stopping send loop");
        }

        private static async Task SendAvro(MqttSessionClient mqttSessionClient, int iterations, TimeSpan interval)
        {
            AvroComm.AvroModel.AvroModel.TelemetrySender telemetrySender = new(mqttSessionClient);

            for (int i = 0; i < iterations; i++)
            {
                Console.WriteLine($"  Sending iteration {i}");
                await telemetrySender.SendTelemetryAsync(new AvroComm.AvroModel.TelemetryCollection
                {
                    Lengths = new List<double>() { i, i + 1, i + 2 },
                    Proximity = i % 3 == 0 ?
                        AvroComm.AvroModel.ProximitySchema.far :
                        AvroComm.AvroModel.ProximitySchema.near,
                    Schedule = new AvroComm.AvroModel.ScheduleSchema
                    {
                        Course = "Math",
                        Credit = new TimeSpan(i + 2, i + 1, i).ToString(),
                    }
                });

                await Task.Delay(interval);
            }
        }

        private static async Task SendJson(MqttSessionClient mqttSessionClient, int iterations, TimeSpan interval)
        {
            JsonComm.JsonModel.JsonModel.TelemetrySender telemetrySender = new(mqttSessionClient);

            for (int i = 0; i < iterations; i++)
            {
                Console.WriteLine($"  Sending iteration {i}");
                await telemetrySender.SendTelemetryAsync(new JsonComm.JsonModel.TelemetryCollection
                {
                    Lengths = new() { i, i + 1, i + 2 },
                    Proximity = i % 3 == 0 ?
                        JsonComm.JsonModel.ProximitySchema.Far :
                        JsonComm.JsonModel.ProximitySchema.Near,
                    Schedule = new JsonComm.JsonModel.ScheduleSchema
                    {
                        Course = "Math",
                        Credit = new TimeSpan(i + 2, i + 1, i),
                    }
                });

                await Task.Delay(interval);
            }
        }

        private static async Task SendRaw(MqttSessionClient mqttSessionClient, int iterations, TimeSpan interval)
        {
            RawComm.RawModel.RawModel.TelemetrySender telemetrySender = new(mqttSessionClient);

            for (int i = 0; i < iterations; i++)
            {
                Console.WriteLine($"  Sending iteration {i}");
                byte[] payload = Encoding.UTF8.GetBytes($"Sample data {i}");
                await telemetrySender.SendTelemetryAsync(payload);

                await Task.Delay(interval);
            }
        }

        private static async Task SendCustom(MqttSessionClient mqttSessionClient, int iterations, TimeSpan interval)
        {
            CustomComm.CustomModel.CustomModel.TelemetrySender telemetrySender = new(mqttSessionClient);

            for (int i = 0; i < iterations; i++)
            {
                Console.WriteLine($"  Sending iteration {i}");
                byte[] payload = Encoding.UTF8.GetBytes($"Sample data {i}");
                await telemetrySender.SendTelemetryAsync(new CustomPayload(payload, "text/csv", MqttPayloadFormatIndicator.CharacterData));

                await Task.Delay(interval);
            }
        }
    }
}
