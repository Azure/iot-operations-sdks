// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.IntegrationTests;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;

namespace Azure.Iot.Operations.Protocol.Session.IntegrationTests;

public class MqttSessionClientIntegrationTests
{
    [Fact]
    public async Task MqttSessionClientCanPublishSubscribeAndUnsubscribe()
    {
        await using MqttSessionClient sessionClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        TaskCompletionSource<MqttApplicationMessage> receivedMessageTcs = new();
        sessionClient.ApplicationMessageReceivedAsync += (args) =>
        {
            receivedMessageTcs.TrySetResult(args.ApplicationMessage);
            return Task.CompletedTask;
        };

        string expectedTopic = "myTopic/" + Guid.NewGuid().ToString();
        MqttClientSubscribeResult subscribeResult = await sessionClient.SubscribeAsync(
            new MqttClientSubscribeOptions(expectedTopic, MqttQualityOfServiceLevel.AtLeastOnce));

        Assert.Single(subscribeResult.Items);
        Assert.Equal(MqttClientSubscribeReasonCode.GrantedQoS1, subscribeResult.Items.First().ReasonCode);

        byte[] expectedPayload = Guid.NewGuid().ToByteArray();
        MqttApplicationMessage outgoingMessage = new MqttApplicationMessage(expectedTopic, MqttQualityOfServiceLevel.AtLeastOnce)
        {
            PayloadSegment = expectedPayload,
        };

        MqttClientPublishResult publishResult = await sessionClient.PublishAsync(outgoingMessage);
        Assert.True(publishResult.IsSuccess);

        MqttApplicationMessage? receivedMessage = null;
        try
        {
            receivedMessage = await receivedMessageTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for the message to be received");
        }

        Assert.NotNull(receivedMessage);
        Assert.Equal(expectedPayload, receivedMessage.PayloadSegment.Array);
        Assert.Equal(expectedTopic, receivedMessage.Topic);

        MqttClientUnsubscribeResult unsubscribeResult =
            await sessionClient.UnsubscribeAsync(new MqttClientUnsubscribeOptions(expectedTopic));

        Assert.Single(unsubscribeResult.Items);
        Assert.Equal(expectedTopic, unsubscribeResult.Items.First().TopicFilter);
        Assert.Equal(MqttClientUnsubscribeReasonCode.Success, unsubscribeResult.Items.First().ReasonCode);
    }

    [Fact]
    public async Task MqttSessionClientCanUseBrokerAssignedClientId()
    {
        await using MqttSessionClient sessionClient = await ClientFactory.CreateSessionClientFromEnvAsync("", true);

        Assert.NotNull(sessionClient.ClientId);
        Assert.NotEmpty(sessionClient.ClientId);
    }
}
