// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Events;
using System.Text;
using k8s;
using System.Text.Json;
using Azure.Iot.Operations.Services.StateStore;
//using System.Text.Json.Nodes;
//using Newtonsoft.Json;

namespace EventDrivenApp;

public class WindowFunctionWorker(MqttSessionClient sessionClient, ILogger<WindowFunctionWorker> logger, IConfiguration configuration) : BackgroundService
{
    private const string PUBSUB_INPUT_TOPIC = "sensor/data";
    private const string PUBSUB_OUTPUT_TOPIC = "sensor/window_data";
    private const string STATESTORE_SENSOR_KEY = "event_app_sample";

    private const string SENSOR_ID = "sensor_id";
    private const string SENSOR_TIMESTAMP = "timestamp";
    private const string SENSOR_TEMPERATURE = "temperature";
    private const string SENSOR_PRESSURE = "pressure";
    private const string SENSOR_VIBRATION = "vibration";
    private const string MSG_NUMBER = "msg_number";

    private const int WINDOW_SIZE = 30;
    private const int PUBLISH_INTERVAL = 10;

    private HashSet<int> trackedSensor = new HashSet<int>();

    public class SensorData
    {
        public int sensor_id { get; set; }
        public DateTime timestamp { get; set; }
        public double temperature { get; set; }
        public double pressure { get; set; }
        public double vibration { get; set; }
        public int msg_number { get; set; }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await Console.Out.WriteLineAsync("Starting");

        try
        {
            sessionClient.ApplicationMessageReceivedAsync += OnMessageReceived;

            MqttConnectionSettings mcs;

            if (KubernetesClientConfiguration.IsInCluster())
            {
                await Console.Out.WriteLineAsync("Running in cluster, load config from environment");
                mcs = MqttConnectionSettings.FromEnvVars();
            }
            else
            {
                await Console.Out.WriteLineAsync("Running locally, load config from connection string");
                mcs = MqttConnectionSettings.FromConnectionString(configuration.GetConnectionString("Default")!);
            }

            await Console.Out.WriteLineAsync($"Connecting to: {mcs}");
            await sessionClient.ConnectAsync(mcs, cancellationToken);

            // subscribe to a topic
            var subscribe = new MqttClientSubscribeOptions("sensor/data", MqttQualityOfServiceLevel.AtLeastOnce);
            await sessionClient.SubscribeAsync(subscribe, cancellationToken);

            // enter the window function loop
            await RunWindow(cancellationToken);

            await sessionClient.DisconnectAsync();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            Environment.Exit(1);
        }
    }

    private async Task<SensorData[]> GetState(StateStoreClient stateStoreClient)
    {
        StateStoreGetResponse? response = await stateStoreClient.GetAsync(STATESTORE_SENSOR_KEY);
        if (response.Value == null)
        {
            return JsonSerializer.Deserialize<SensorData[]>("[]")!;
        }

        return JsonSerializer.Deserialize<SensorData[]>(response.Value.GetString())!;
    }

    private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(args.ApplicationMessage.PayloadSegment);
            {
                await Console.Out.WriteLineAsync($"Received {document.RootElement.GetRawText()}");

                JsonElement root = document.RootElement;

                // Extract timestamp
                DateTime timestamp = root.GetProperty(SENSOR_TIMESTAMP).GetDateTime();

                int msgNumber = root.GetProperty(MSG_NUMBER).GetInt32();

                // Track the sensor for publishing window
                trackedSensor.Add(root.GetProperty(SENSOR_ID).GetInt32());


                await using StateStoreClient stateStoreClient = new StateStoreClient(sessionClient);
                {
                    SensorData[] sensors = await GetState(stateStoreClient);
                    sensors.Append(JsonSerializer.Deserialize<SensorData>(root.GetRawText())!);

                    // var state = await stateStoreClient.GetAsync(STATESTORE_SENSOR_KEY);
                    // if (state.Value != null)
                    // {
                    //     var newState = state.Value.GetString() + root.GetRawText();
                    //     await stateStoreClient.SetAsync(STATESTORE_SENSOR_KEY, newState);
                    // }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }

        return;
    }

    private async Task RunWindow(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Fetch state

            // Calculate window

            // Expire old data

            // Set state

            // Publish window
            await sessionClient.PublishAsync(
                new MqttApplicationMessage("sensor/window_data")
                {
                    PayloadSegment = Encoding.UTF8.GetBytes("hello"),
                },
                cancellationToken);

            // Wait for 10 seconds
            await Task.Delay(10000, cancellationToken);
        }
    }
}
