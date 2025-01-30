// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Globalization;
using System.Text;
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
            Json,
            Raw
        }

        const string avroClientId = "AvroDotnetClient";
        const string jsonClientId = "JsonDotnetClient";
        const string rawClientId = "RawDotnetClient";

        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: Client {AVRO|JSON|RAW} seconds_to_run");
                return;
            }

            (CommFormat format, string clientId) = args[0].ToLowerInvariant() switch
            {
                "avro" => (CommFormat.Avro, avroClientId),
                "json" => (CommFormat.Json, jsonClientId),
                "raw" => (CommFormat.Raw, rawClientId),
                _ => throw new ArgumentException("format must be AVRO or JSON or RAW", nameof(args))
            };

            TimeSpan runDuration = TimeSpan.FromSeconds(int.Parse(args[1], CultureInfo.InvariantCulture));

            MqttSessionClient mqttSessionClient = new();

            Console.Write($"Connecting to MQTT broker as {clientId} ... ");
            await mqttSessionClient.ConnectAsync(new MqttConnectionSettings("localhost") { TcpPort = 1883, UseTls = false, ClientId = clientId });
            Console.WriteLine("Connected!");

            Console.WriteLine("Starting receive loop");
            Console.WriteLine();

            switch (format)
            {
                case CommFormat.Avro:
                    await ReceiveAvro(mqttSessionClient, runDuration);
                    break;
                case CommFormat.Json:
                    await ReceiveJson(mqttSessionClient, runDuration);
                    break;
                case CommFormat.Raw:
                    await ReceiveRaw(mqttSessionClient, runDuration);
                    break;
            }

            Console.WriteLine("Stopping receive loop");
        }

        private static async Task ReceiveAvro(MqttSessionClient mqttSessionClient, TimeSpan runDuration)
        {
            AvroComm.AvroModel.AvroModel.TelemetryReceiver telemetryCollectionReceiver = new(mqttSessionClient);

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

        private static async Task ReceiveJson(MqttSessionClient mqttSessionClient, TimeSpan runDuration)
        {
            JsonComm.JsonModel.JsonModel.TelemetryReceiver telemetryCollectionReceiver = new(mqttSessionClient);

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

        private static async Task ReceiveRaw(MqttSessionClient mqttSessionClient, TimeSpan runDuration)
        {
            RawComm.RawModel.RawModel.TelemetryReceiver telemetryCollectionReceiver = new(mqttSessionClient);

            telemetryCollectionReceiver.OnTelemetryReceived += (sender, telemetry, metadata) =>
            {
                Console.WriteLine($"Received telemetry from {sender}....");

                if (telemetry != null)
                {
                    string data = Encoding.UTF8.GetString(telemetry);
                    Console.WriteLine($"  Data: \"{data}\"");
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
