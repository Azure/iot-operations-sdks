// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;

namespace Server
{
    internal sealed class Program
    {
        private enum CommFormat
        {
            Avro,
            Json
        }

        const string avroServerId = "AvroDotnetServer";
        const string jsonServerId = "JsonDotnetServer";

        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: Server {AVRO|JSON} iterations [interval_in_seconds]");
                return;
            }

            CommFormat format = args[0].ToLowerInvariant() switch
            {
                "avro" => CommFormat.Avro,
                "json" => CommFormat.Json,
                _ => throw new ArgumentException("format must be AVRO or JSON", nameof(args))
            };

            string serverId = format == CommFormat.Avro ? avroServerId : jsonServerId;

            int iterations = int.Parse(args[1], CultureInfo.InvariantCulture);

            TimeSpan interval = TimeSpan.FromSeconds(args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 1);

            MqttSessionClient mqttSessionClient = new();

            Console.Write($"Connecting to MQTT broker as {serverId} ... ");
            await mqttSessionClient.ConnectAsync(new MqttConnectionSettings("localhost") { TcpPort = 1883, UseTls = false, ClientId = serverId });
            Console.WriteLine("Connected!");

            Console.WriteLine("Starting send loop");
            Console.WriteLine();

            if (format == CommFormat.Avro)
            {
                await SendAvro(mqttSessionClient, iterations, interval);
            }
            else
            {
                await SendJson(mqttSessionClient, iterations, interval);
            }

            Console.WriteLine();
            Console.WriteLine("Stopping send loop");
        }

        private static async Task SendAvro(MqttSessionClient mqttSessionClient, int iterations, TimeSpan interval)
        {
            AvroComm.dtmi_codegen_communicationTest_avroModel__1.AvroModel.TelemetryCollectionSender telemetryCollectionSender = new(mqttSessionClient);

            for (int i = 0; i < iterations; i++)
            {
                Console.WriteLine($"  Sending iteration {i}");
                await telemetryCollectionSender.SendTelemetryAsync(new AvroComm.dtmi_codegen_communicationTest_avroModel__1.TelemetryCollection
                {
                    Lengths = new List<double>() { i, i + 1, i + 2 },
                    Proximity = i % 3 == 0 ?
                        AvroComm.dtmi_codegen_communicationTest_avroModel__1.Enum_Proximity.far :
                        AvroComm.dtmi_codegen_communicationTest_avroModel__1.Enum_Proximity.near,
                    Schedule = new AvroComm.dtmi_codegen_communicationTest_avroModel__1.Object_Schedule
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
            JsonComm.dtmi_codegen_communicationTest_jsonModel__1.JsonModel.TelemetryCollectionSender telemetryCollectionSender = new(mqttSessionClient);

            for (int i = 0; i < iterations; i++)
            {
                Console.WriteLine($"  Sending iteration {i}");
                await telemetryCollectionSender.SendTelemetryAsync(new JsonComm.dtmi_codegen_communicationTest_jsonModel__1.TelemetryCollection
                {
                    Lengths = new() { i, i + 1, i + 2 },
                    Proximity = i % 3 == 0 ?
                        JsonComm.dtmi_codegen_communicationTest_jsonModel__1.Enum_Proximity.Far :
                        JsonComm.dtmi_codegen_communicationTest_jsonModel__1.Enum_Proximity.Near,
                    Schedule = new JsonComm.dtmi_codegen_communicationTest_jsonModel__1.Object_Schedule
                    {
                        Course = "Math",
                        Credit = new TimeSpan(i + 2, i + 1, i),
                    }
                });

                await Task.Delay(interval);
            }
        }
    }
}
