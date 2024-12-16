// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Mqtt.Session;
using System.Text;
using TestEnvoys.Greeter;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class SharedSubscriptionIntegrationTests
{
    [Fact]
    public async Task SharedSubscription2ClientTest()
    {
        // set up clients
        await using MqttSessionClient publisherClient = await ClientFactory.CreateSessionClientFromEnvAsync("pubClient");
        await using MqttSessionClient client1 = await ClientFactory.CreateSessionClientFromEnvAsync("client1");
        await using MqttSessionClient client2 = await ClientFactory.CreateSessionClientFromEnvAsync("client2");

        Assert.True(client1.IsConnected, "Client1 did not connect.");
        Assert.True(client2.IsConnected, "Client2 did not connect.");

        // set up shared subscription
        string topicFilter = Guid.NewGuid().ToString();
        string sharedTopic = "$share/basic-shared/" + topicFilter;
        TaskCompletionSource<MqttApplicationMessage> receivedMessageTcs1 = new();
        TaskCompletionSource<MqttApplicationMessage> receivedMessageTcs2 = new();

        client1.ApplicationMessageReceivedAsync += (e) =>
        {
            receivedMessageTcs1.TrySetResult(e.ApplicationMessage);
            return Task.CompletedTask;
        };

        client2.ApplicationMessageReceivedAsync += (e) =>
        {
            receivedMessageTcs2.TrySetResult(e.ApplicationMessage);
            return Task.CompletedTask;
        };

        MqttClientSubscribeResult subscribeResult1 = await client1.SubscribeAsync(
            new MqttClientSubscribeOptions(sharedTopic, MqttQualityOfServiceLevel.AtLeastOnce));
        MqttClientSubscribeResult subscribeResult2 = await client2.SubscribeAsync(
            new MqttClientSubscribeOptions(sharedTopic, MqttQualityOfServiceLevel.AtLeastOnce));

        Assert.Single(subscribeResult1.Items);
        Assert.Equal(MqttClientSubscribeReasonCode.GrantedQoS1, subscribeResult1.Items.First().ReasonCode);

        Assert.Single(subscribeResult2.Items);
        Assert.Equal(MqttClientSubscribeReasonCode.GrantedQoS1, subscribeResult2.Items.First().ReasonCode);

        Assert.True(client1.IsConnected, "Client1 disconnected after sub.");
        Assert.True(client2.IsConnected, "Client2 disconnected after sub.");

        // publish message
        var message = new MqttApplicationMessage(topicFilter, MqttQualityOfServiceLevel.AtLeastOnce)
        {
            PayloadSegment = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()),
        };

        MqttClientPublishResult publishResult = await publisherClient.PublishAsync(message);
        Assert.True(publishResult.IsSuccess, "Publish did not succeed.");

        // verify that at least one, but only one, client received the message);
        var receivedMessage = await Task.WhenAny(receivedMessageTcs1.Task, receivedMessageTcs2.Task).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(receivedMessage == receivedMessageTcs1.Task || receivedMessage == receivedMessageTcs2.Task, "At least one client should receive the message.");
        Assert.True(receivedMessage == receivedMessageTcs1.Task ^ receivedMessage == receivedMessageTcs2.Task, "Only one client should receive the message.");
    }
}