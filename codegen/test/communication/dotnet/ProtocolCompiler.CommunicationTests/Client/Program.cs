﻿using System;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;

namespace Client
{
    internal sealed class Program
    {
        private enum CommFormat
        {
            Avro,
            Json
        }

        const string avroClientId = "AvroDotnetClient";
        const string jsonClientId = "JsonDotnetClient";

        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: Client {AVRO|JSON} seconds_to_run");
                return;
            }

            CommFormat format = args[0].ToLowerInvariant() switch
            {
                "avro" => CommFormat.Avro,
                "json" => CommFormat.Json,
                _ => throw new ArgumentException("format must be AVRO or JSON", nameof(args))
            };

            string clientId = format == CommFormat.Avro ? avroClientId : jsonClientId;

            TimeSpan runDuration = TimeSpan.FromSeconds(int.Parse(args[1], CultureInfo.InvariantCulture));

            MqttSessionClient mqttSessionClient = new();

            Console.Write($"Connecting to MQTT broker as {clientId} ... ");
            await mqttSessionClient.ConnectAsync(new MqttConnectionSettings("localhost") { TcpPort = 1883, UseTls = false, ClientId = clientId });
            Console.WriteLine("Connected!");

            Console.WriteLine("Starting receive loop");
            Console.WriteLine();

            if (format == CommFormat.Avro)
            {
                await ReceiveAvro(mqttSessionClient, runDuration);
            }
            else
            {
                await ReceiveJson(mqttSessionClient, runDuration);
            }

            Console.WriteLine("Stopping receive loop");
        }

        private static async Task ReceiveAvro(MqttSessionClient mqttSessionClient, TimeSpan runDuration)
        {
            AvroComm.dtmi_codegen_communicationTest_avroModel__1.AvroModel.TelemetryCollectionReceiver telemetryCollectionReceiver = new(mqttSessionClient);

            telemetryCollectionReceiver.OnTelemetryReceived += (sender, telemetry, metadata) =>
            {
                Console.WriteLine($"Received telemetry from {sender}....");

                if (telemetry.schedule != null)
                {
                    Console.WriteLine($"  Schedule: course \"{telemetry.schedule.course}\" => {telemetry.schedule.credit}");
                }

                if (telemetry.lengths != null)
                {
                    Console.WriteLine($"  Lengths: {string.Join(", ", telemetry.lengths.Select(l => l.ToString(CultureInfo.InvariantCulture)))}");
                }

                if (telemetry.proximity != null)
                {
                    Console.WriteLine($"  Proximity: {telemetry.proximity}");
                }

                Console.WriteLine();

                return Task.CompletedTask;
            };

            await telemetryCollectionReceiver.StartAsync();

            await Task.Delay(runDuration);

            await telemetryCollectionReceiver.StopAsync();
        }

        private static async Task ReceiveJson(MqttSessionClient mqttSessionClient, TimeSpan runDuration)
        {
            JsonComm.dtmi_codegen_communicationTest_jsonModel__1.JsonModel.TelemetryCollectionReceiver telemetryCollectionReceiver = new(mqttSessionClient);

            telemetryCollectionReceiver.OnTelemetryReceived += (sender, telemetry, metadata) =>
            {
                Console.WriteLine($"Received telemetry from {sender}....");

                if (telemetry.Schedule != null)
                {
                    Console.WriteLine($"  Schedule: course \"{telemetry.Schedule.Course}\" => {telemetry.Schedule.Credit}");
                }

                if (telemetry.Lengths != null)
                {
                    Console.WriteLine($"  Lengths: {string.Join(", ", telemetry.Lengths.Select(l => l.ToString(CultureInfo.InvariantCulture)))}");
                }

                if (telemetry.Proximity != null)
                {
                    Console.WriteLine($"  Proximity: {telemetry.Proximity}");
                }

                Console.WriteLine();

                return Task.CompletedTask;
            };

            await telemetryCollectionReceiver.StartAsync();

            await Task.Delay(runDuration);

            await telemetryCollectionReceiver.StopAsync();
        }
    }
}
