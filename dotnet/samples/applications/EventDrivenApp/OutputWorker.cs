// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.StateStore;
using System.Text.Json;

namespace EventDrivenApp;

public class OutputWorker(ApplicationContext applicationContext, SessionClientFactory clientFactory, ILogger<InputWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get a session client
            var sessionClient = await clientFactory.GetSessionClient("output");

            // enter the window function loop
            await ProcessWindow(sessionClient, cancellationToken);

            await sessionClient.DisconnectAsync();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            Environment.Exit(1);
        }
    }

    private async Task ProcessWindow(MqttSessionClient sessionClient, CancellationToken cancellationToken)
    {
        JsonSerializerOptions serializeOptions = new() { WriteIndented = true };
        WindowTelemetrySender sender = new(applicationContext, sessionClient);

        while (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Processing window");

            DateTime timeNow = DateTime.UtcNow;
            List<SensorData> inputData = [];

            try
            {
                // Wait before processing the next window
                await Task.Delay(Constants.PublishInterval * 1000, cancellationToken);

                await using StateStoreClient stateStoreClient = new(applicationContext, sessionClient);
                {
                    // Fetch the past sensor data from the state store
                    IStateStoreGetResponse response = await stateStoreClient.GetAsync(Constants.StateStoreSensorKey);
                    if (response.Value == null)
                    {
                        await Console.Out.WriteLineAsync("Sensor data not found in state store");
                        continue;
                    }

                    // Deserialize the sensor data
                    inputData = JsonSerializer.Deserialize<List<SensorData>>(response.Value.GetString()) ?? [];
                }

                // Remove older data
                inputData.RemoveAll(d => timeNow - d.Timestamp > TimeSpan.FromSeconds(Constants.WindowSize));
                if (inputData.Count == 0)
                {
                    continue;
                }

                // Calculate window aggregation
                WindowData outputData = new()
                {
                    Timestamp = timeNow,
                    WindowSize = Constants.WindowSize,
                    Temperature = AggregateSensor(inputData, s => s.Temperature),
                    Pressure = AggregateSensor(inputData, s => s.Pressure),
                    Vibration = AggregateSensor(inputData, s => s.Vibration)
                };

                await sender.SendTelemetryAsync(outputData, null, MqttQualityOfServiceLevel.AtMostOnce, cancellationToken: cancellationToken);

                logger.LogInformation("Published window data: {data}", JsonSerializer.Serialize(outputData, serializeOptions));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }
    }

    private WindowSensorData AggregateSensor(List<SensorData> data, Func<SensorData, double> selector)
    {
        return new WindowSensorData
        {
            Min = data.Min(selector),
            Max = data.Max(selector),
            Mean = data.Average(selector),
            Medium = data.OrderBy(selector).ElementAt(data.Count / 2).Vibration,
            Count = data.Count
        };
    }
}
