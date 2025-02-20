// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt;
using System.Text;

namespace ConnectionManagementSample
{
    public class UserManagedConnectionWorker(OrderedAckMqttClient mqttClient, ILogger<UserManagedConnectionWorker> logger, IConfiguration configuration) : BackgroundService
    {
        readonly SemaphoreSlim _reconnectionMutex = new (1);

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(configuration.GetConnectionString("Default")! + ";ClientId=UserManagedConnectionClient-" + Guid.NewGuid());

            mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;
            mqttClient.DisconnectedAsync += OnDisconnect;

            bool connected = false;
            while (!connected)
            { 
                MqttClientConnectResult connectResult = await mqttClient.ConnectAsync(mcs, cancellationToken);
                connected = connectResult.ResultCode == MqttClientConnectResultCode.Success;

                // Actual application retry logic should be more robust than this (some errors shouldn't be retried, for example)
                // but this sample won't demonstrate that logic for the sake of brevity.
                if (!connected)
                {
                    logger.LogWarning("Failed to connect with reason code: {c}. Will attempt to connect again.", connectResult.ResultCode);
                    await Task.Delay(1000, cancellationToken);
                }
            }

            var subscribe = new MqttClientSubscribeOptions("userManagedConnectionSampleTopic/hello", MqttQualityOfServiceLevel.AtLeastOnce);
            await mqttClient.SubscribeAsync(subscribe, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await mqttClient.PublishAsync(
                        new MqttApplicationMessage("libraryManagedConnectionSampleTopic/hello")
                        { 
                            PayloadSegment = Encoding.UTF8.GetBytes("hello from the user-managed connection sample!"),
                        }, 
                        cancellationToken);
                }
                catch (Exception e)
                {
                    // Note that this operation can fail if the connection is lost after this operation starts and before
                    // this operation finishes. An actual application would have actual retry logic here, but it has been omitted
                    // for brevity.
                    logger.LogError("Failed to send a publish: {e}", e);
                }
                
                await Task.Delay(10000, cancellationToken);
            }
        }

        // Unlike with the session client, users that want to manage the connection themselves must handle all
        // disconnects regardless of if they are transient or fatal.
        private async Task OnDisconnect(MqttClientDisconnectedEventArgs args)
        {
            _reconnectionMutex.Wait();
            try
            {
                // This callback can be called multiple times even if reconnection is already happening. In those cases,
                // the semaphore should prevent multiple reconnection threads and this check should prevent reconnection
                // from happening after another thread already reconnected.
                if (mqttClient.IsConnected)
                {
                    return;
                }

                logger.LogInformation("MQTT client lost its connection due to {e}", args.Exception);

                // This is an example of simple (but not very robust) reconnection logic.
                while (true)
                {
                    try
                    {
                        // This sample is configured to recover the MQTT session via the CleanStart and SessionExpiry settings in the 
                        // connection string. However, if you configure it to reconnect with a clean session, then you will need to
                        // manually re-subscribe to any topics that you wish to continue receiving on.
                        await mqttClient.ReconnectAsync();
                        break; // reconnect succeeded, so stop trying to reconnect
                    }
                    catch (Exception ex)
                    {
                        // Note that this exception may be a fatal exception (for example, a protocol violation occurred)
                        // and not worth retrying. For the sake of brevity, logic for discerning fatal vs retryable is omitted
                        // from this sample. Note that the session client does contain logic for doing this, though.
                        logger.LogError("Tried to reconnect, but failed. {e}", ex);
                        await Task.Delay(1000);
                    }
                }
            }
            finally 
            {
                _reconnectionMutex.Release();
            }
        }

        private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
        {
            logger.LogInformation("Received a message with topic {t} with payload {p}", args.ApplicationMessage.Topic, args.ApplicationMessage.ConvertPayloadToString());

            // You can also acknowledge a message manually later via the args.AcknowledgeAsync() API
            args.AutoAcknowledge = true;

            return Task.CompletedTask;
        }
    }
}
