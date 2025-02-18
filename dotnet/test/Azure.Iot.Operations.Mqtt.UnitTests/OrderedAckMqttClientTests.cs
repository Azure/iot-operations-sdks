// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt;
using MQTTnet.Exceptions;
using System.Text;

namespace Azure.Iot.Operations.Protocol.UnitTests;

public class OrderedAckMqttClientTests
{
    [Fact]
    public async Task CreateWithFactoryExtension()
    {
        await using OrderedAckMqttClient clientNoLogger = new OrderedAckMqttClient(new MQTTnet.MqttFactory().CreateMqttClient());
        Assert.NotNull(clientNoLogger);

        await using OrderedAckMqttClient clientWithLogger = new OrderedAckMqttClient(new MQTTnet.MqttFactory().CreateMqttClient(MqttNetTraceLogger.CreateTraceLogger()));
        Assert.NotNull(clientWithLogger);
    }

    [Fact]
    public async Task OrderedAckMqttClient_ConnectCallsConnect()
    {
        using MockMqttClient mockMqttClient = new MockMqttClient();
        await using OrderedAckMqttClient orderedAckMqttClient = new(mockMqttClient);

        MqttClientOptions expectedOptions = new MqttClientOptions(new MqttClientTcpOptions("some MQTT broker uri", 9999))
        {
            AuthenticationMethod = "some method",
            AuthenticationData = new byte[] { 1, 1, 1 },
            CleanSession = true,
            ClientId = Guid.NewGuid().ToString(),
            KeepAlivePeriod = TimeSpan.FromSeconds(15),
            MaximumPacketSize = 5,
            AllowPacketFragmentation = false,
            ReceiveMaximum = 10,
            RequestProblemInformation = true,
            RequestResponseInformation = true,
            SessionExpiryInterval = 15,
            ProtocolVersion = MqttProtocolVersion.V500,
            WillContentType = "application/json",
            WillCorrelationData = new byte[] { 1, 2, 3, 4 },
            WillPayload = new byte[] { 5, 6, 7, 8 },
            WillPayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
            WillResponseTopic = "some/will/responseTopic",
            WillTopic = "some/will/topic",
            WillRetain = true,
        };

        expectedOptions.AddUserProperty("Some user property key", "Some user property value");

        mockMqttClient.OnConnectAttempt += (actualOptions) =>
        {
            return Task.FromResult(mockMqttClient.CompareExpectedConnectWithActual(expectedOptions, actualOptions, false));
        };

        MqttClientConnectResult connectResult = await orderedAckMqttClient.ConnectAsync(expectedOptions);
        Assert.False(connectResult.IsSessionPresent);
        Assert.Equal(MqttClientConnectResultCode.Success, connectResult.ResultCode);
    }

    [Fact]
    public async Task OrderedAckMqttClient_DisconnectCallsDisconnect()
    {
        using MockMqttClient mockMqttClient = new MockMqttClient();
        await using OrderedAckMqttClient orderedAckMqttClient = new(mockMqttClient);

        MqttClientDisconnectOptions expectedOptions = new MqttClientDisconnectOptions()
        {
            Reason = MqttClientDisconnectOptionsReason.ProtocolError,
            ReasonString = "some reason",
            SessionExpiryInterval = 11,
        };

        expectedOptions.AddUserProperty("someUserPropertyKey", "someUserPropertyValue");

        mockMqttClient.OnDisconnectAttempt += (actualOptions) =>
        {
            MockMqttClient.CompareExpectedDisconnectWithActual(expectedOptions, actualOptions);
            return Task.CompletedTask;
        };

        await orderedAckMqttClient.DisconnectAsync(expectedOptions);
    }

    [Fact]
    public async Task OrderedAckMqttClient_PublishCallsPublish()
    {
        using MockMqttClient mockMqttClient = new MockMqttClient();
        await using OrderedAckMqttClient orderedAckMqttClient = new(mockMqttClient);

        MqttApplicationMessage expectedPublish = new MqttApplicationMessage("some/topic", MqttQualityOfServiceLevel.AtLeastOnce)
        {
            PayloadSegment = new byte[] { 1, 2, 3 },
            PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
            ContentType = "some content type",
            CorrelationData = new byte[] { 3, 4, 5 },
            MessageExpiryInterval = 12,
            ResponseTopic = "some/response/topic",
            Retain = true,
            SubscriptionIdentifiers = new List<uint> { 3, 4 },
            TopicAlias = 38,
        };

        expectedPublish.AddUserProperty("someUserPropertyKey", "someUserPropertyValue");

        mockMqttClient.OnPublishAttempt += (actualPublish) =>
        {
            return Task.FromResult(MockMqttClient.CompareExpectedPublishWithActual(expectedPublish, actualPublish));
        };

        MqttClientPublishResult result =
            await orderedAckMqttClient.PublishAsync(expectedPublish);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task OrderedAckMqttClient_SubscribeCallsSubscribe()
    {
        using MockMqttClient mockMqttClient = new MockMqttClient();
        await using OrderedAckMqttClient orderedAckMqttClient = new(mockMqttClient);

        MqttTopicFilter mqttTopicFilter = new MqttTopicFilter("some/enqueued/topic/filter", MqttQualityOfServiceLevel.AtLeastOnce)
        {
            RetainAsPublished = true,
            NoLocal = false,
            RetainHandling = MqttRetainHandling.DoNotSendOnSubscribe
        };

        MqttClientSubscribeOptions expectedSubscribe = new MqttClientSubscribeOptions(mqttTopicFilter)
        {
            SubscriptionIdentifier = 44,
        };

        expectedSubscribe.AddUserProperty("someUserPropertyKey", "someUserPropertyValue");

        mockMqttClient.OnSubscribeAttempt += (actualSubscribe) =>
        {
            return Task.FromResult(MockMqttClient.CompareExpectedSubscribeWithActual(expectedSubscribe, actualSubscribe));
        };

        MqttClientSubscribeResult result =
            await orderedAckMqttClient.SubscribeAsync(expectedSubscribe);

        Assert.True(result.IsSubAckSuccessful(MqttQualityOfServiceLevel.AtLeastOnce));
    }

    [Fact]
    public async Task OrderedAckMqttClient_UnsubscribeCallsUnsubscribe()
    {
        using MockMqttClient mockMqttClient = new MockMqttClient();
        await using OrderedAckMqttClient orderedAckMqttClient = new(mockMqttClient);

        MqttClientUnsubscribeOptions expectedUnsubscribe = new MqttClientUnsubscribeOptions("some/enqueued/topic/filter");
        expectedUnsubscribe.AddUserProperty("someUserPropertyKey", "someUserPropertyValue");

        mockMqttClient.OnUnsubscribeAttempt += (actualUnsubscribe) =>
        {
            return Task.FromResult(MockMqttClient.CompareExpectedUnsubscribeWithActual(expectedUnsubscribe, actualUnsubscribe));
        };

        MqttClientUnsubscribeResult result =
            await orderedAckMqttClient.UnsubscribeAsync(expectedUnsubscribe);

        Assert.True(result.IsUnsubAckSuccessful());
    }

    [Fact]
    public async Task OrderedAckMqttClient_ReceivedPublish()
    {
        using MockMqttClient mockMqttClient = new MockMqttClient();
        await using OrderedAckMqttClient orderedAckMqttClient = new(mockMqttClient);

        TaskCompletionSource<MqttApplicationMessageReceivedEventArgs> messageReceivedTcs = new();
        orderedAckMqttClient.ApplicationMessageReceivedAsync += (args) =>
        {
            messageReceivedTcs.TrySetResult(args);
            return Task.CompletedTask;
        };

        await orderedAckMqttClient.ConnectAsync(GetDefaultClientOptions());

        MQTTnet.MqttApplicationMessage expectedMessage = new MQTTnet.MqttApplicationMessageBuilder()
                .WithPayload(new byte[] { 1, 2, 3 })
                .WithContentType("some content type")
                .WithCorrelationData(new byte[] { 3, 4, 5 })
                .WithMessageExpiryInterval(12)
                .WithPayloadFormatIndicator(MQTTnet.Protocol.MqttPayloadFormatIndicator.CharacterData)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithResponseTopic("some/response/topic")
                .WithRetainFlag(true)
                .WithSubscriptionIdentifier(34)
                .WithTopic("some/topic")
                .WithTopicAlias(38)
                .WithUserProperty("someUserPropertyKey", "someUserPropertyValue")
                .Build();

        ushort expectedPacketId = 12;
        await mockMqttClient.SimulateNewMessageAsync(expectedMessage, expectedPacketId);

        MqttApplicationMessageReceivedEventArgs? actualMessage = null;
        try
        {
            actualMessage = await messageReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for a message to be received");
        }

        Assert.NotNull(actualMessage);
        MockMqttClient.CompareExpectedReceivedPublishWithActual(expectedMessage, expectedPacketId, actualMessage);
    }

    [Fact]
    public async Task OrderedAckMqttClient_PubackQueueClearedIfConnectionLost()
    {
        using MockMqttClient mockClient = new MockMqttClient();
        await using OrderedAckMqttClient orderedAckMqttClient = new(mockClient);

        try
        {
            TaskCompletionSource<MqttApplicationMessageReceivedEventArgs> messageReceivedTcs = new();
            orderedAckMqttClient.ApplicationMessageReceivedAsync += async (args) =>
            {
                args.AutoAcknowledge = false;
                await mockClient.SimulateServerInitiatedDisconnectAsync(new MqttCommunicationException("some error"));
                messageReceivedTcs.TrySetResult(args);
            };

            await orderedAckMqttClient.ConnectAsync(GetDefaultClientOptions());

            string expectedTopic = "some/topic";
            MqttClientSubscribeOptions expectedSubscribe = new MqttClientSubscribeOptions(expectedTopic);

            await orderedAckMqttClient.SubscribeAsync(expectedSubscribe);

            MQTTnet.MqttApplicationMessage message = new MQTTnet.MqttApplicationMessageBuilder()
                .WithTopic(expectedTopic)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await mockClient.SimulateNewMessageAsync(message);

            MqttApplicationMessageReceivedEventArgs msg = await messageReceivedTcs.Task;

            // When ack'ing a QoS 1+ message from the client, this function will typically return prior to the
            // acknowledgement being sent
            await msg.AcknowledgeAsync(CancellationToken.None);

            // Because of the above, wait a bit before checking what messages have/have not been acknowledged according to the mock
            // MQTT client.
            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.Empty(mockClient.AcknowledgedMessages);
        }
        finally
        {
            await orderedAckMqttClient.DisconnectAsync();
        }
    }

    [Fact]
    public async Task OrderedAckMqttClient_PubacksAreOrdered()
    {
        using MockMqttClient mockClient = new MockMqttClient();
        await using OrderedAckMqttClient orderedAckMqttClient = new(mockClient);

        try
        {
            TaskCompletionSource<MqttApplicationMessageReceivedEventArgs> message1ReceivedTcs = new();
            TaskCompletionSource<MqttApplicationMessageReceivedEventArgs> message2ReceivedTcs = new();
            int messageReceivedCount = 0;
            orderedAckMqttClient.ApplicationMessageReceivedAsync += (args) =>
            {
                args.AutoAcknowledge = false;
                messageReceivedCount++;

                if (messageReceivedCount == 1)
                {
                    message1ReceivedTcs.TrySetResult(args);
                }
                else if (messageReceivedCount == 2)
                {
                    message2ReceivedTcs.TrySetResult(args);
                }

                return Task.CompletedTask;
            };

            await orderedAckMqttClient.ConnectAsync(GetDefaultClientOptions());

            string expectedTopic = "some/topic";
            MqttClientSubscribeOptions expectedSubscribe = new MqttClientSubscribeOptions(expectedTopic);

            await orderedAckMqttClient.SubscribeAsync(expectedSubscribe);

            TaskCompletionSource OnReconnectComplete = new();
            mockClient.OnConnectAttempt += (actualOptions) =>
            {
                _ = Task.Run(() =>
                {
                    Task.Delay(TimeSpan.FromSeconds(1));
                    OnReconnectComplete.TrySetResult();
                });

                return Task.FromResult(new MQTTnet.Client.MqttClientConnectResult());
            };

            TaskCompletionSource<MQTTnet.MqttApplicationMessage> firstMessageAcknowledged = new();
            TaskCompletionSource<MQTTnet.MqttApplicationMessage> secondMessageAcknowledged = new();
            int ackCount = 0;
            mockClient.OnPublishAcknowledged += (message) =>
            {
                ackCount++;
                if (ackCount == 1)
                {
                    firstMessageAcknowledged.TrySetResult(message);
                }
                else if (ackCount == 2)
                {
                    secondMessageAcknowledged.TrySetResult(message);
                }
                return Task.CompletedTask;
            };

            MQTTnet.MqttApplicationMessage message1 = new MQTTnet.MqttApplicationMessageBuilder()
                .WithTopic(expectedTopic)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithCorrelationData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()))
                .Build();
            MQTTnet.MqttApplicationMessage message2 = new MQTTnet.MqttApplicationMessageBuilder()
                .WithTopic(expectedTopic)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithCorrelationData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()))
                .Build();

            await mockClient.SimulateNewMessageAsync(message1);
            await mockClient.SimulateNewMessageAsync(message2);

            MqttApplicationMessageReceivedEventArgs msg1 = await message1ReceivedTcs.Task;
            MqttApplicationMessageReceivedEventArgs msg2 = await message2ReceivedTcs.Task;

            // Acknowledge only the second received message
            await msg2.AcknowledgeAsync(CancellationToken.None);

            // While the client should not send an ack yet since the application hasn't ack'd msg1, wait a bit before
            // checking what messages have/have not been acknowledged according to the mock MQTT client.
            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.Empty(mockClient.AcknowledgedMessages);

            // When ack'ing a QoS 1+ message from the client, this function will typically return prior to the
            // acknowledgement being sent
            await msg1.AcknowledgeAsync(CancellationToken.None);

            // Because of the above, wait a bit before checking what messages have/have not been acknowledged according to the mock
            // MQTT client.
            await firstMessageAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await secondMessageAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(30));

            // Now that both messages were acknowledged from the application layer, the client should have sent both
            // acknowledgements in the same order that they were received in.
            Assert.Equal(2, mockClient.AcknowledgedMessages.Count);
            Assert.Equal(message1.CorrelationData, mockClient.AcknowledgedMessages[0].ApplicationMessage.CorrelationData);
            Assert.Equal(message2.CorrelationData, mockClient.AcknowledgedMessages[1].ApplicationMessage.CorrelationData);
        }
        finally
        {
            await orderedAckMqttClient.DisconnectAsync();
        }
    }

    [Fact]
    public async Task OrderedAckMqttClient_ReceivedPublishDoesNotCrashClientIfCallbackThrows()
    {
        using MockMqttClient mockMqttClient = new MockMqttClient();
        await using OrderedAckMqttClient orderedAckMqttClient = new(mockMqttClient);
        await orderedAckMqttClient.ConnectAsync(GetDefaultClientOptions());

        try
        {
            orderedAckMqttClient.ApplicationMessageReceivedAsync += (args) =>
            {
                throw new Exception("Failed to process a received publish");
            };

            MQTTnet.MqttApplicationMessage expectedMessage = new MQTTnet.MqttApplicationMessageBuilder()
                    .WithTopic("some/topic")
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                    .Build();

            // This method would throw if the MqttNet client didn't catch the thrown exception.
            await mockMqttClient.SimulateNewMessageAsync(expectedMessage, 12);
        }
        finally
        {
            await orderedAckMqttClient.DisconnectAsync();
        }
    }

    [Fact]
    public async Task OrderedAckMqttClient_ReceivedPublishStillSendsAcknowledgementIfCallbackThrows()
    {
        using MockMqttClient mockMqttClient = new MockMqttClient();
        await using OrderedAckMqttClient orderedAckMqttClient = new(mockMqttClient);

        await orderedAckMqttClient.ConnectAsync(GetDefaultClientOptions());
        try
        {
            orderedAckMqttClient.ApplicationMessageReceivedAsync += (args) =>
            {
                throw new Exception("Failed to process a received publish");
            };

            TaskCompletionSource<MQTTnet.MqttApplicationMessage> acknowledgedMessage = new();
            mockMqttClient.OnPublishAcknowledged += (message) =>
            {
                acknowledgedMessage.TrySetResult(message);
                return Task.CompletedTask;
            };

            var expectedMessage = new MQTTnet.MqttApplicationMessageBuilder()
                    .WithTopic("some/topic")
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

            // This method would throw if the MqttNet client didn't catch the thrown exception.
            await mockMqttClient.SimulateNewMessageAsync(expectedMessage, 12);

            // It may take a second for the acknowledgement thread on the client to actually acknowledge the publish even
            // with autoack
            Assert.Equal(expectedMessage, await acknowledgedMessage.Task.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.Single(mockMqttClient.AcknowledgedMessages);
        }
        finally
        {
            await orderedAckMqttClient.DisconnectAsync();
        }
    }

    [Fact]
    public async Task OrderedAckMqttClient_ThrowsIfAccessedWhenDisposed()
    {
        using MockMqttClient mockMqttClient = new MockMqttClient();
        await using OrderedAckMqttClient orderedAckMqttClient = new(mockMqttClient);

        await orderedAckMqttClient.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await orderedAckMqttClient.ConnectAsync(GetDefaultClientOptions()));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await orderedAckMqttClient.DisconnectAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await orderedAckMqttClient.PublishAsync(new MqttApplicationMessage("sometopic")));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await orderedAckMqttClient.SubscribeAsync(new MqttClientSubscribeOptions("someTopic")));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await orderedAckMqttClient.UnsubscribeAsync(new MqttClientUnsubscribeOptions("someTopic")));
    }

    [Fact]
    public async Task OrderedAckMqttClient_ThrowsIfCancellationRequested()
    {
        using MockMqttClient mockMqttClient = new MockMqttClient();
        await using OrderedAckMqttClient orderedAckMqttClient = new(mockMqttClient);

        CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await orderedAckMqttClient.ConnectAsync(GetDefaultClientOptions(), cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await orderedAckMqttClient.DisconnectAsync(cancellationToken: cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await orderedAckMqttClient.PublishAsync(new MqttApplicationMessage("sometopic"), cancellationToken: cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await orderedAckMqttClient.SubscribeAsync(new MqttClientSubscribeOptions("someTopic"), cancellationToken: cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await orderedAckMqttClient.UnsubscribeAsync(new MqttClientUnsubscribeOptions("someTopic"), cancellationToken: cts.Token));
    }

    private MqttClientOptions GetDefaultClientOptions()
    {
        return new MqttClientOptions(new MqttClientTcpOptions("host", 1883));
    }

    [Fact]
    public async Task OrderedAckMqttClient_ThrowsIfExceedsMaxPacketSize()
    {
        using MockMqttClient mockMqttClient = new MockMqttClient();
        await using OrderedAckMqttClient mqttNetClient = new(mockMqttClient);

        MqttClientOptions options = new MqttClientOptions(new MqttClientTcpOptions("some MQTT broker uri", 9999))
        {
            ClientId = Guid.NewGuid().ToString(),
            MaximumPacketSize = 5,
        };

        await mqttNetClient.ConnectAsync(options);

        MqttApplicationMessage message = new MqttApplicationMessage("some/topic")
        {
            PayloadSegment = new byte[options.MaximumPacketSize + 1]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await mqttNetClient.PublishAsync(message));
    }

    [Fact]
    public async Task OrderedAckMqttClient_GetClientIdWithoutAnAssignedClientIdReturnsNull()
    {
        using MockMqttClient mockMqttClient = new MockMqttClient();
        await using OrderedAckMqttClient orderedAckMqttClient = new(mockMqttClient);

        // Previously, this would throw a NPE. This test implicitly asserts that no exception is thrown in this case.
        Assert.Null(orderedAckMqttClient.ClientId);
    }
}