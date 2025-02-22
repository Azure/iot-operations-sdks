// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol.Telemetry;
using System.Text.Json;
using System.Collections.Concurrent;
using Azure.Iot.Operations.Protocol;
using EventDrivenApp.Models;
using EventDrivenApp.Telemetry;

namespace EventDrivenApp;

public class InputWorker(ApplicationContext applicationContext, SessionClientFactory clientFactory, ILogger<InputWorker> logger) : BackgroundService
{
    private readonly BlockingCollection<SensorData> _incomingSensorData = [];

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get a connected session client
            MqttSessionClient sessionClient = await clientFactory.GetSessionClient("input");

            // Start the telemetry receiver
            var receiver = new SensorTelemetryReceiver(applicationContext, sessionClient)
            {
                OnTelemetryReceived = ReceiveTelemetry
            };
            await receiver.StartAsync(cancellationToken);

            // Enter main loop to process the sensor data
            await ProcessSensorData(sessionClient, cancellationToken);

            await sessionClient.DisconnectAsync(null, cancellationToken);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            Environment.Exit(1);
        }
    }

    private Task ReceiveTelemetry(string senderId, SensorData sensor, IncomingTelemetryMetadata metadata)
    {
        logger.LogInformation($"Received sensor data");

        _incomingSensorData.Add(sensor);

        return Task.CompletedTask;
    }

    private async Task ProcessSensorData(MqttSessionClient sessionClient, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            SensorData sensor = _incomingSensorData.Take(cancellationToken);

            logger.LogInformation("Processing sensor data");

            List<SensorData> data = [];

            await using StateStoreClient stateStoreClient = new(applicationContext, sessionClient);
            {
                try
                {
                    // Fetch the historical sensor data from the state store
                    StateStoreGetResponse response = await stateStoreClient.GetAsync(Constants.StateStoreSensorKey, null, cancellationToken);
                    if (response.Value != null)
                    {
                        data = JsonSerializer.Deserialize<List<SensorData>>(response.Value.GetString()) ?? [];
                    }

                    // Discard old data
                    DateTime timeNow = DateTime.UtcNow;
                    data.RemoveAll(d => timeNow - d.Timestamp > TimeSpan.FromSeconds(Constants.WindowSize));
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Unable to deserialize state store data, deleting the key");
                    await stateStoreClient.DeleteAsync(Constants.StateStoreSensorKey, null, null, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fetch state store data");
                }

                try
                {
                    // Drain the incoming queue
                    do
                    {
                        data.Add(sensor);
                    } while (_incomingSensorData.TryTake(out sensor!));

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
