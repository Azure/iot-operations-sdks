// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Newtonsoft.Json;
using System.Text;

namespace EventDrivenApp;

public class OutputWorker(MqttSessionClient sessionClient, MqttConnectionSettings connectionSettings, ILogger<OutputWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Connecting to: {settings}", connectionSettings);

            await sessionClient.ConnectAsync(connectionSettings, cancellationToken);

            // enter the window function loop
            await ProcessWindow(cancellationToken);

            await sessionClient.DisconnectAsync();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            Environment.Exit(1);
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

                // Wait before processing the next window
                await Task.Delay(Constants.PublishInterval * 1000, cancellationToken);

                await using StateStoreClient stateStoreClient = new(sessionClient);
                {
                    // Fetch the past sensor data from the state store
                    StateStoreGetResponse response = await stateStoreClient.GetAsync(Constants.StateStoreSensorKey);
                    if (response.Value == null)
                    {
                        await Console.Out.WriteLineAsync("Sensor data not found in state store");
                        continue;
                    }

                    // Deserialize the sensor data
                    inputData = JsonConvert.DeserializeObject<List<InputSensorData>>(response.Value.GetString()) ?? [];
                }

                // Remove older data
                inputData.RemoveAll(d => timeNow - d.Timestamp > TimeSpan.FromSeconds(Constants.WindowSize));

                if (inputData.Count == 0)
                {
                    continue;
                }

                // Calculate window aggregation
                var outputData = new OutputSensorData()
                {
                    Timestamp = timeNow,
                    WindowSize = Constants.WindowSize,
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
                    new MqttApplicationMessage(Constants.OutputTopic)
                    {
                        PayloadSegment = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(outputData, Formatting.Indented)),
                    },
                    cancellationToken);

                logger.LogInformation("Published window data: {data}", JsonConvert.SerializeObject(outputData, Formatting.Indented));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }
    }
}
