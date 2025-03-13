// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt;
using System.Buffers;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class OrderedAckMqttClientIntegrationTests
{
    [Fact]
    public async Task OrderedAckMqttClientCanPublishSubscribeAndUnsubscribe()
    {
        await using OrderedAckMqttClient mqttClient = await ClientFactory.CreateClientAsyncFromEnvAsync(Guid.NewGuid().ToString());

        TaskCompletionSource<MqttApplicationMessage> receivedMessageTcs = new();
        mqttClient.ApplicationMessageReceivedAsync += (args) =>
        {
            receivedMessageTcs.TrySetResult(args.ApplicationMessage);
            return Task.CompletedTask;
        };

        string expectedTopic = "myTopic/" + Guid.NewGuid().ToString();
        MqttClientSubscribeResult subscribeResult = await mqttClient.SubscribeAsync(
            new MqttClientSubscribeOptions(expectedTopic, MqttQualityOfServiceLevel.AtLeastOnce));

        Assert.Single(subscribeResult.Items);
        Assert.Equal(MqttClientSubscribeReasonCode.GrantedQoS1, subscribeResult.Items.First().ReasonCode);

        byte[] expectedPayload = Guid.NewGuid().ToByteArray();
        MqttApplicationMessage outgoingMessage = new MqttApplicationMessage(expectedTopic, MqttQualityOfServiceLevel.AtLeastOnce)
        {
            PayloadSegment = expectedPayload
        };

        MqttClientPublishResult publishResult = await mqttClient.PublishAsync(outgoingMessage);
        Assert.True(publishResult.IsSuccess);

        MqttApplicationMessage? receivedMessage = null;
        try
        {
            receivedMessage = await receivedMessageTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for the message to be received");
        }

        Assert.NotNull(receivedMessage);
        Assert.Equal(expectedPayload, receivedMessage.Payload.ToArray());
        Assert.Equal(expectedTopic, receivedMessage.Topic);

        MqttClientUnsubscribeResult unsubscribeResult =
            await mqttClient.UnsubscribeAsync(new MqttClientUnsubscribeOptions(expectedTopic));

        Assert.Single(unsubscribeResult.Items);
        Assert.Equal(expectedTopic, unsubscribeResult.Items.First().TopicFilter);
        Assert.Equal(MqttClientUnsubscribeReasonCode.Success, unsubscribeResult.Items.First().ReasonCode);

        await mqttClient.DisconnectAsync();
    }

    [Fact]
    public async Task OrderedAckMqttClientCanSendPubacksOutOfOrder()
    {
        await using OrderedAckMqttClient mqttClient = await ClientFactory.CreateClientAsyncFromEnvAsync(Guid.NewGuid().ToString());

        TaskCompletionSource<MqttApplicationMessageReceivedEventArgs> receivedMessage1Tcs = new();
        TaskCompletionSource<MqttApplicationMessageReceivedEventArgs> receivedMessage2Tcs = new();
        int messagesReceived = 0;
        mqttClient.ApplicationMessageReceivedAsync += (args) =>
        {
            messagesReceived++;

            if (messagesReceived == 1)
            {
                receivedMessage1Tcs.TrySetResult(args);
            }
            else if (messagesReceived == 2)
            {
                receivedMessage2Tcs.TrySetResult(args);
            }

            return Task.CompletedTask;
        };

        string expectedTopic = "myTopic/" + Guid.NewGuid().ToString();
        MqttClientSubscribeResult subscribeResult = await mqttClient.SubscribeAsync(
            new MqttClientSubscribeOptions(expectedTopic, MqttQualityOfServiceLevel.AtLeastOnce));

        Assert.Single(subscribeResult.Items);
        Assert.Equal(MqttClientSubscribeReasonCode.GrantedQoS1, subscribeResult.Items.First().ReasonCode);

        byte[] expectedPayload = Guid.NewGuid().ToByteArray();
        MqttApplicationMessage outgoingMessage = new MqttApplicationMessage(expectedTopic, MqttQualityOfServiceLevel.AtLeastOnce)
        {
            PayloadSegment = expectedPayload,
        };

        MqttClientPublishResult publish1Result = await mqttClient.PublishAsync(outgoingMessage);
        Assert.True(publish1Result.IsSuccess);
        MqttClientPublishResult publish2Result = await mqttClient.PublishAsync(outgoingMessage);
        Assert.True(publish2Result.IsSuccess);

        MqttApplicationMessageReceivedEventArgs? receivedMessage1 = null;
        try
        {
            receivedMessage1 = await receivedMessage1Tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for the message to be received");
        }

        MqttApplicationMessageReceivedEventArgs? receivedMessage2 = null;
        try
        {
            receivedMessage2 = await receivedMessage2Tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for the message to be received");
        }

        Assert.NotNull(receivedMessage1);
        Assert.NotNull(receivedMessage2);

        int disconnectCount = 0;
        mqttClient.DisconnectedAsync += (args) =>
        {
            disconnectCount++;
            return Task.CompletedTask;
        };

        // Ack the messages out of order. If the MqttNetClient did not order these
        // for you, then the broker should kill the MQTT connection.
        await receivedMessage2.AcknowledgeAsync(CancellationToken.None);
        await receivedMessage1.AcknowledgeAsync(CancellationToken.None);

        // Wait a bit to ensure the broker has a chance to kill the connection if the ACKs were sent
        // in the wrong order
        await Task.Delay(TimeSpan.FromSeconds(3));

        Assert.Equal(0, disconnectCount);

        await mqttClient.DisconnectAsync();
    }
}
