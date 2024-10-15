// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol.Telemetry;
using System.Text.Json;

namespace EventDrivenApp;

public class InputWorker(SessionClientFactory clientFactory, ILogger<InputWorker> logger) : BackgroundService
{
    private MqttSessionClient? sessionClient = null;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get a session client
            sessionClient = await clientFactory.GetSessionClient("input");

            // Start the telemetry receiver
            var receiver = new SensorTelemetryReceiver(sessionClient)
            {
                OnTelemetryReceived = ReceiveTelemetry
            };
            await receiver.StartAsync(cancellationToken);

            // Wait forever
            await Task.Delay(Timeout.Infinite, cancellationToken);

            await sessionClient.DisconnectAsync(null, cancellationToken);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            Environment.Exit(1);
        }
    }

    private async Task ReceiveTelemetry(string senderId, SensorData sensor, IncomingTelemetryMetadata metadata)
    {
        logger.LogInformation($"Received sensor data from {senderId}", senderId);

        List<SensorData> data = [];
        await using StateStoreClient stateStoreClient = new(sessionClient!);
        {
            try
            {
                // Fetch the historical sensor data from the state store
                StateStoreGetResponse response = await stateStoreClient.GetAsync(Constants.StateStoreSensorKey, null);
                if (response.Value != null)
                {
                    data = JsonSerializer.Deserialize<List<SensorData>>(response.Value.GetString()) ?? [];

                    // Discard old data
                    DateTime timeNow = DateTime.UtcNow;
                    data.RemoveAll(d => timeNow - d.Timestamp > TimeSpan.FromSeconds(Constants.WindowSize));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get state store data, deleting the key");
                await stateStoreClient.DeleteAsync(Constants.StateStoreSensorKey, null, null);
            }

            try
            {
                // Add the new sensor data
                data.Add(sensor);

                // Push the sensor data back to the state store
                await stateStoreClient.SetAsync(Constants.StateStoreSensorKey, JsonSerializer.Serialize(data), null, null);

                logger.LogDebug("State store contains {count} items", data.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }
    }
}
