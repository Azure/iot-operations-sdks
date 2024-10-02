// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using k8s;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Text;

namespace EventDrivenApp;

public class WindowFunctionWorker(MqttSessionClient sessionClient, ILogger<WindowFunctionWorker> logger, IConfiguration configuration) : BackgroundService
{
    private const string PUBSUB_INPUT_TOPIC = "sensor/data";
    private const string PUBSUB_OUTPUT_TOPIC = "sensor/window_data";
    private const string STATESTORE_SENSOR_KEY = "event_app_sample";

    private const int WINDOW_SIZE = 60;
    private const int PUBLISH_INTERVAL = 10;

    private BlockingCollection<InputSensorData> incomingSensorData = [];

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
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
            var subscribe = new MqttClientSubscribeOptions(PUBSUB_INPUT_TOPIC, MqttQualityOfServiceLevel.AtLeastOnce);
            await sessionClient.SubscribeAsync(subscribe, cancellationToken);

            // enter the window function loop
            var tasks = new List<Task>
            {
                ProcessInputSensorData(cancellationToken),
                ProcessWindow(cancellationToken)
            };
            await Task.WhenAll(tasks);

            await sessionClient.DisconnectAsync();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            Environment.Exit(1);
        }
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        args.AutoAcknowledge = true;

        try
        {
            // Ignore other topics
            if (args.ApplicationMessage.Topic != PUBSUB_INPUT_TOPIC)
            {
                return Task.CompletedTask;
            }

            logger.LogInformation($"Received message on topic {args.ApplicationMessage.Topic}");

            // Deserialize the incoming sensor data
            var sensorData = JsonConvert.DeserializeObject<InputSensorData>(args.ApplicationMessage.ConvertPayloadToString()!);
            if (sensorData == null)
            {
                logger.LogError("Failed to deserialize sensor payload");
                return Task.CompletedTask;
            }

            // Add the sensor data to the incoming queue
            incomingSensorData.Add(sensorData);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }

        return Task.CompletedTask;
    }

    private async Task ProcessInputSensorData(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var sensorData = incomingSensorData.Take(cancellationToken);

                await using StateStoreClient stateStoreClient = new(sessionClient);
                {
                    // Fetch the past sensor data from the state store
                    StateStoreGetResponse response = await stateStoreClient.GetAsync(STATESTORE_SENSOR_KEY);
                    List<InputSensorData> data = [];
                    if (response.Value != null)
                    {
                        data = JsonConvert.DeserializeObject<List<InputSensorData>>(response.Value.GetString()) ?? [];
                    }

                    // Append the new sensor data
                    data.Add(sensorData);

                    // Set the sensor data back to the state store
                    await stateStoreClient.SetAsync(STATESTORE_SENSOR_KEY, JsonConvert.SerializeObject(data), null, null, cancellationToken);
                    logger.LogDebug($"State store contains {data.Count} items");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }
    }

    private async Task ProcessWindow(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var timeNow = DateTime.UtcNow;
            var inputData = new List<InputSensorData>();

            try
            {
                logger.LogDebug("Processing window");

                // Wait for 10 seconds before processing the next window
                await Task.Delay(PUBLISH_INTERVAL * 1000, cancellationToken);

                await using StateStoreClient stateStoreClient = new(sessionClient);
                {
                    // Fetch the past sensor data from the state store
                    StateStoreGetResponse response = await stateStoreClient.GetAsync(STATESTORE_SENSOR_KEY);
                    if (response.Value == null)
                    {
                        await Console.Out.WriteLineAsync("Sensor data not found in state store");
                        throw new Exception("Sensor data not found in state store");
                    }

                    // Deserialize the sensor data
                    inputData = JsonConvert.DeserializeObject<List<InputSensorData>>(response.Value.GetString()) ?? [];

                    // Discard old data by iterating over a copy of the list
                    var discardCount = 0;
                    foreach (InputSensorData sensor in inputData.ToList())
                    {
                        if (timeNow - sensor.Timestamp > TimeSpan.FromSeconds(WINDOW_SIZE))
                        {
                            inputData.Remove(sensor);
                            ++discardCount;
                        }
                    }
                    logger.LogDebug($"Discarded {discardCount} sensor data");

                    // Store the pruned state back to the state store
                    await stateStoreClient.SetAsync(STATESTORE_SENSOR_KEY, JsonConvert.SerializeObject(inputData));
                }

                if (inputData.Count == 0)
                {
                    continue;
                }

                // Calculate window aggregation
                var outputData = new OutputSensorData()
                {
                    Timestamp = timeNow,
                    WindowSize = WINDOW_SIZE,
                    Temperature = new OutputSensorData.AggregatedSensorData
                    {
                        Min = inputData.Min(s => s.Temperature),
                        Max = inputData.Max(s => s.Temperature),
                        Mean = inputData.Average(s => s.Temperature),
                        Medium = inputData.OrderBy(s => s.Temperature).ElementAt(inputData.Count / 2).Temperature,
                        Count = inputData.Count
                    },
                    Pressure = new OutputSensorData.AggregatedSensorData
                    {
                        Min = inputData.Min(s => s.Pressure),
                        Max = inputData.Max(s => s.Pressure),
                        Mean = inputData.Average(s => s.Pressure),
                        Medium = inputData.OrderBy(s => s.Pressure).ElementAt(inputData.Count / 2).Pressure,
                        Count = inputData.Count
                    },
                    Vibration = new OutputSensorData.AggregatedSensorData
                    {
                        Min = inputData.Min(s => s.Vibration),
                        Max = inputData.Max(s => s.Vibration),
                        Mean = inputData.Average(s => s.Vibration),
                        Medium = inputData.OrderBy(s => s.Vibration).ElementAt(inputData.Count / 2).Vibration,
                        Count = inputData.Count
                    }
                };

                // Publish window data
                await sessionClient.PublishAsync(
                    new MqttApplicationMessage(PUBSUB_OUTPUT_TOPIC)
                    {
                        PayloadSegment = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(outputData)),
                    },
                    cancellationToken);

                logger.LogInformation($"Published window data: {JsonConvert.SerializeObject(outputData, Formatting.Indented)}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }
    }
}
