// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;

namespace Client
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

        const string avroClientId = "AvroDotnetClient";
        const string jsonClientId = "JsonDotnetClient";
        const string rawClientId = "RawDotnetClient";
        const string customClientId = "CustomDotnetClient";

        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: TelemClient {AVRO|JSON|RAW|CUSTOM} seconds_to_run");
                return;
            }

            (CommFormat format, string clientId) = args[0].ToLowerInvariant() switch
            {
                "avro" => (CommFormat.Avro, avroClientId),
                "json" => (CommFormat.Json, jsonClientId),
                "raw" => (CommFormat.Raw, rawClientId),
                "custom" => (CommFormat.Custom, customClientId),
                _ => throw new ArgumentException("format must be AVRO or JSON or RAW or CUSTOM", nameof(args))
            };

            TimeSpan runDuration = TimeSpan.FromSeconds(int.Parse(args[1], CultureInfo.InvariantCulture));

            ApplicationContext appContext = new();
            MqttSessionClient mqttSessionClient = new();

            Console.Write($"Connecting to MQTT broker as {clientId} ... ");
            await mqttSessionClient.ConnectAsync(new MqttConnectionSettings("localhost", clientId) { TcpPort = 1883, UseTls = false });
            Console.WriteLine("Connected!");

            Console.WriteLine("Starting receive loop");
            Console.WriteLine();

            switch (format)
            {
                case CommFormat.Avro:
                    await ReceiveAvro(appContext, mqttSessionClient, runDuration);
                    break;
                case CommFormat.Json:
                    await ReceiveJson(appContext, mqttSessionClient, runDuration);
                    break;
                case CommFormat.Raw:
                    await ReceiveRaw(appContext, mqttSessionClient, runDuration);
                    break;
                case CommFormat.Custom:
                    await ReceiveCustom(appContext, mqttSessionClient, runDuration);
                    break;
            }

            Console.WriteLine("Stopping receive loop");
        }

        private static async Task ReceiveAvro(ApplicationContext appContext, MqttSessionClient mqttSessionClient, TimeSpan runDuration)
        {
            AvroComm.AvroModel.AvroModel.TelemetryReceiver telemetryReceiver = new(appContext, mqttSessionClient);

            telemetryReceiver.OnTelemetryReceived += (sender, telemetry, metadata) =>
            {
                Console.WriteLine($"Received telemetry from {sender} with token replacement {metadata.TopicTokens["ex:myToken"]}....");

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

                if (telemetry.Data != null)
                {
                    Console.WriteLine($"  Data: \"{Encoding.UTF8.GetString(telemetry.Data)}\"");
                }

                Console.WriteLine();

                return Task.CompletedTask;
            };

            await telemetryReceiver.StartAsync();

            await Task.Delay(runDuration);

            await telemetryReceiver.StopAsync();
        }

        private static async Task ReceiveJson(ApplicationContext appContext, MqttSessionClient mqttSessionClient, TimeSpan runDuration)
        {
            JsonComm.JsonModel.JsonModel.TelemetryReceiver telemetryReceiver = new(appContext, mqttSessionClient);

            telemetryReceiver.OnTelemetryReceived += (sender, telemetry, metadata) =>
            {
                Console.WriteLine($"Received telemetry from {sender} with token replacement {metadata.TopicTokens["ex:myToken"]}....");

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

                if (telemetry.Data != null)
                {
                    Console.WriteLine($"  Data: \"{Encoding.UTF8.GetString(telemetry.Data)}\"");
                }

                Console.WriteLine();

                return Task.CompletedTask;
            };

            await telemetryReceiver.StartAsync();

            await Task.Delay(runDuration);

            await telemetryReceiver.StopAsync();
        }

        private static async Task ReceiveRaw(ApplicationContext appContext, MqttSessionClient mqttSessionClient, TimeSpan runDuration)
        {
            RawComm.RawModel.RawModel.TelemetryReceiver telemetryReceiver = new(appContext, mqttSessionClient);

            telemetryReceiver.OnTelemetryReceived += (sender, telemetry, metadata) =>
            {
                Console.WriteLine($"Received telemetry from {sender} with token replacement {metadata.TopicTokens["ex:myToken"]}....");

                if (telemetry != null)
                {
                    string data = Encoding.UTF8.GetString(telemetry);
                    Console.WriteLine($"  Data: \"{data}\"");
                }

                Console.WriteLine();

                return Task.CompletedTask;
            };

            await telemetryReceiver.StartAsync();

            await Task.Delay(runDuration);

            await telemetryReceiver.StopAsync();
        }

        private static async Task ReceiveCustom(ApplicationContext appContext, MqttSessionClient mqttSessionClient, TimeSpan runDuration)
        {
            CustomComm.CustomModel.CustomModel.TelemetryReceiver telemetryReceiver = new(appContext, mqttSessionClient);

            telemetryReceiver.OnTelemetryReceived += (sender, telemetry, metadata) =>
            {
                Console.WriteLine($"Received telemetry from {sender} with content type {telemetry.ContentType} and token replacement {metadata.TopicTokens["ex:myToken"]}....");

                if (telemetry != null)
                {
                    string data = Encoding.UTF8.GetString(telemetry.SerializedPayload!);
                    Console.WriteLine($"  Payload: \"{data}\"");
                }

                Console.WriteLine();

                return Task.CompletedTask;
            };

            await telemetryReceiver.StartAsync();

            await Task.Delay(runDuration);

            await telemetryReceiver.StopAsync();
        }
    }
}
