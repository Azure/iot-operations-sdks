// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Newtonsoft.Json;
using System.Collections.Concurrent;

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
            var subscribe = new MqttClientSubscribeOptions(Constants.InputTopic, MqttQualityOfServiceLevel.AtLeastOnce);
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

    private async Task ProcessInputData(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var sensorData = incomingSensorData.Take(cancellationToken);

                await using StateStoreClient stateStoreClient = new(sessionClient);
                {
                    // Fetch the historical sensor data from the state store
                    StateStoreGetResponse response = await stateStoreClient.GetAsync(Constants.StateStoreSensorKey, null, cancellationToken);
                    List<InputSensorData> data = [];
                    if (response.Value != null)
                    {
                        data = JsonConvert.DeserializeObject<List<InputSensorData>>(response.Value.GetString()) ?? [];
                    }

                    // Discard old data
                    var timeNow = DateTime.UtcNow;
                    data.RemoveAll(d => timeNow - d.Timestamp > TimeSpan.FromSeconds(Constants.WindowSize));

                    // Drain the incoming queue
                    do
                    {
                        data.Add(sensorData);
                    } while (incomingSensorData.TryTake(out sensorData));

                    // Push the sensor data back to the state store
                    await stateStoreClient.SetAsync(Constants.StateStoreSensorKey, JsonConvert.SerializeObject(data), null, null, cancellationToken);
                    logger.LogDebug("State store contains {count} items", data.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }
    }
}
