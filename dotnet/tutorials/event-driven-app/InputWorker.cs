// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace EventDrivenApp;

public class InputWorker(MqttSessionClient sessionClient, MqttConnectionSettings connectionSettings, ILogger<InputWorker> logger) : BackgroundService
{
    private BlockingCollection<InputSensorData> incomingSensorData = [];

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Connecting to: {settings}", connectionSettings);

            sessionClient.ApplicationMessageReceivedAsync += OnMessageReceived;

            await sessionClient.ConnectAsync(connectionSettings, cancellationToken);

            // subscribe to a topic
            MqttClientSubscribeOptions subscribe = new(Constants.InputTopic, MqttQualityOfServiceLevel.AtLeastOnce);
            await sessionClient.SubscribeAsync(subscribe, cancellationToken);

            // enter the process input loop
            await ProcessInputData(cancellationToken);

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

        // Only process the topic we care about (ignore state store responses)
        if (args.ApplicationMessage.Topic != Constants.InputTopic)
        {
            return Task.CompletedTask;
        }

        logger.LogInformation($"Received message on topic {args.ApplicationMessage.Topic}");

        try
        {
            // Deserialize the incoming sensor data
            InputSensorData? sensorData = JsonSerializer.Deserialize<InputSensorData>(args.ApplicationMessage.ConvertPayloadToString()!);
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

    private async Task ProcessInputData(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            InputSensorData sensorData = incomingSensorData.Take(cancellationToken);

            await using StateStoreClient stateStoreClient = new(sessionClient);
            {
                List<InputSensorData> data = [];

                try
                {
                    // Fetch the historical sensor data from the state store
                    StateStoreGetResponse response = await stateStoreClient.GetAsync(Constants.StateStoreSensorKey, null, cancellationToken);
                    if (response.Value != null)
                    {
                        data = JsonSerializer.Deserialize<List<InputSensorData>>(response.Value.GetString()) ?? [];
                    }

                    // Discard old data
                    DateTime timeNow = DateTime.UtcNow;
                    data.RemoveAll(d => timeNow - d.Timestamp > TimeSpan.FromSeconds(Constants.WindowSize));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "State store contained invalid data, deleting the key");
                    await stateStoreClient.DeleteAsync(Constants.StateStoreSensorKey, null, null, cancellationToken);
                }

                try
                {
                    // Drain the incoming queue
                    do
                    {
                        data.Add(sensorData);
                    } while (incomingSensorData.TryTake(out sensorData));

                    // Push the sensor data back to the state store
                    await stateStoreClient.SetAsync(Constants.StateStoreSensorKey, JsonSerializer.Serialize(data), null, null, cancellationToken);
                    logger.LogDebug("State store contains {count} items", data.Count);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, ex.Message);
                }
            }
        }
    }
}
