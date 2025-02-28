// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using Azure.Iot.Operations.Protocol.UnitTests;
using Azure.Iot.Operations.Protocol.Retry;
using Azure.Iot.Operations.Protocol.Events;
using MQTTnet.Exceptions;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Mqtt;
using Azure.Iot.Operations.Mqtt.Session.Exceptions;

namespace Azure.Iot.Operations.Protocol.Session.UnitTests
{
    public class MqttSessionClientTests
    {
        [Theory]
        [ClassData(typeof(UnsuccessfulAndUnretriableConnackCodesClassData))]
        public async Task MqttSessionClient_InitialConnectDoesNotRetryOnExpectedUnsuccessfulConnectCodes(MQTTnet.Protocol.MqttConnectReasonCode unsuccessfulConnectReasonCode)
        {
            using MockMqttClient mockClient = new MockMqttClient();
            MqttSessionClientOptions options = new()
            {
                RetryOnFirstConnect = true,
            };
            await using MqttSessionClient sessionClient = new(mockClient, options);

            try
            {
                int attemptNumber = 0;
                mockClient.OnConnectAttempt += (actualConnect) =>
                {
                    attemptNumber++;
                    if (attemptNumber == 1)
                    {
                        var packet = new MQTTnet.Packets.MqttConnAckPacket()
                        {
                            ReasonCode = unsuccessfulConnectReasonCode,
                        };

                        return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(packet, MQTTnet.Formatter.MqttProtocolVersion.V500));
                    }
                    else
                    {
                        return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulInitialConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500));
                    }
                };

                var thrownException = await Assert.ThrowsAsync<MqttConnectingFailedException>(async () => await sessionClient.ConnectAsync(GetClientOptions()));
                Assert.Equal((int)unsuccessfulConnectReasonCode, (int)thrownException.ResultCode); // MQTTNet has both a "MqttClientConnectResult" and a "MqttConnectReasonCode" but they are identical besides the enum name
                Assert.Equal(1, attemptNumber);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Theory]
        [ClassData(typeof(UnsuccessfulButRetriableConnackCodesClassData))]
        public async Task MqttSessionClient_InitialConnectRetriesOnExpectedUnsuccessfulConnectCodes(MQTTnet.Protocol.MqttConnectReasonCode unsuccessfulConnectReasonCode)
        {
            using MockMqttClient mockClient = new MockMqttClient();
            int attemptNumber = 0;
            mockClient.OnConnectAttempt += (actualConnect) =>
            {
                attemptNumber++;
                if (attemptNumber == 1)
                {
                    var packet = new MQTTnet.Packets.MqttConnAckPacket()
                    {
                        ReasonCode = unsuccessfulConnectReasonCode,
                    };

                    return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(packet, MQTTnet.Formatter.MqttProtocolVersion.V500));
                }
                else
                {
                    return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulInitialConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500));
                }
            };

            MqttSessionClientOptions options = new()
            {
                RetryOnFirstConnect = true,
            };
            await using MqttSessionClient sessionClient = new(mockClient, options);

            try
            {
                MqttClientConnectResult connectResult = await sessionClient.ConnectAsync(GetClientOptions());
                Assert.False(connectResult.IsSessionPresent);
                Assert.Equal(MqttClientConnectResultCode.Success, connectResult.ResultCode);
                Assert.Equal(2, attemptNumber);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Theory]
        [ClassData(typeof(UnsuccessfulButRetriableDisconnectReasons))]
        public async Task MqttSessionClient_DisconnectHandlerRetriesOnExpectedUnsuccessfulDisconnectReasons(MQTTnet.MqttClientDisconnectReason disconnectReason)
        {
            using MockMqttClient mockClient = new MockMqttClient();

            TaskCompletionSource onReconnected = new();
            mockClient.OnConnectAttempt += (args) =>
            {
                if (args.CleanSession)
                {
                    return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulInitialConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500));
                }
                else
                {
                    onReconnected.TrySetResult();
                    return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulReconnectConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500));
                }
            };

            await using MqttSessionClient sessionClient = new(mockClient);
            bool sessionLost = false;
            sessionClient.SessionLostAsync += (args) =>
            {
                sessionLost = true;
                return Task.CompletedTask;
            };

            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());
                await mockClient.SimulateServerInitiatedDisconnectAsync(new Exception(), disconnectReason);

                await onReconnected.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.False(sessionLost);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Theory]
        [ClassData(typeof(UnsuccessfulAndUnretriableDisconnectReasons))]
        public async Task MqttSessionClient_DisconnectHandlerDoesNotRetryOnFatalDisconnectReasons(MQTTnet.MqttClientDisconnectReason disconnectReason)
        {
            if (disconnectReason == MQTTnet.MqttClientDisconnectReason.BadAuthenticationMethod)
            {
                // MQTTnet erroneously defines this as a disconnect code even though it is only a connect code. Our SDK
                // doesn't handle this erroneous disconnect code, so skip this test
                return;
            }

            using MockMqttClient mockClient = new MockMqttClient();

            bool reconnectionAttempted = false;
            mockClient.OnConnectAttempt += (args) =>
            {
                if (args.CleanSession)
                {
                    return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulInitialConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500));
                }
                else
                {
                    reconnectionAttempted = true;
                    return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulReconnectConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500));
                }
            };

            await using MqttSessionClient sessionClient = new(mockClient);
            TaskCompletionSource onSessionLostReported = new();
            sessionClient.SessionLostAsync += (args) =>
            {
                onSessionLostReported.TrySetResult();
                return Task.CompletedTask;
            };

            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());
                await mockClient.SimulateServerInitiatedDisconnectAsync(new Exception(), disconnectReason);

                await onSessionLostReported.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.False(reconnectionAttempted);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_DisconnectHandlerRetriesOnExpectedUnsuccessfulConnectException()
        {
            using MockMqttClient mockClient = new MockMqttClient();

            TaskCompletionSource onReconnected = new();
            int reconnectAttemptCount = 0;
            mockClient.OnConnectAttempt += (args) =>
            {
                if (args.CleanSession)
                {
                    return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulInitialConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500));
                }
                else
                {
                    reconnectAttemptCount++;

                    // A session client can encounter any of these exceptions while attempting to reconnect
                    // and it should treat them as retryable
                    if (reconnectAttemptCount == 1)
                    {
                        throw new OperationCanceledException();
                    }
                    else if (reconnectAttemptCount == 2)
                    {
                        throw new TaskCanceledException();
                    }
                    else if (reconnectAttemptCount == 3)
                    {
                        throw new MqttCommunicationException(new Exception());
                    }
                    else if (reconnectAttemptCount == 4)
                    {
                        throw new MqttCommunicationTimedOutException();
                    }

                    onReconnected.TrySetResult();
                    return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulReconnectConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500));
                }
            };

            await using MqttSessionClient sessionClient = new(mockClient);
            bool sessionLost = false;
            sessionClient.SessionLostAsync += (args) =>
            {
                sessionLost = true;
                return Task.CompletedTask;
            };

            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());
                await mockClient.SimulateServerInitiatedDisconnectAsync(new Exception("Some transient network error"), MQTTnet.MqttClientDisconnectReason.NormalDisconnection);

                await onReconnected.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.False(sessionLost);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_DisconnectHandlerAbandonsRetryIfUserDisconnects()
        {
            using MockMqttClient mockClient = new MockMqttClient();

            bool testDisconnectedTheClient = false;
            TaskCompletionSource reconnectOccurredAfterTestDisconnectedTheClient = new();
            mockClient.OnConnectAttempt += (args) =>
            {
                // Allow the initial connect to succeed but reject any reconnection attempts
                if (args.CleanSession)
                {
                    return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulInitialConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500));
                }
                else
                {
                    if (testDisconnectedTheClient)
                    {
                        // This should not happen since the client should not continue
                        // retrying after the user decides to disconnect it.
                        reconnectOccurredAfterTestDisconnectedTheClient.TrySetResult();
                    }

                    throw new OperationCanceledException();
                }
            };

            MqttSessionClientOptions options = new MqttSessionClientOptions()
            {
                ConnectionRetryPolicy = new TestRetryPolicy(int.MaxValue, TimeSpan.FromSeconds(1))
            };

            await using MqttSessionClient sessionClient = new(mockClient);

            await sessionClient.ConnectAsync(GetClientOptions());
            await mockClient.SimulateServerInitiatedDisconnectAsync(new Exception("Some transient network error"), MQTTnet.MqttClientDisconnectReason.NormalDisconnection);
            await sessionClient.DisconnectAsync();

            // It may take a second or two for the last retry attempt to be cancelled, so don't start
            // monitoring if reconnection attempts were made immediately.
            await Task.Delay(TimeSpan.FromSeconds(3));

            testDisconnectedTheClient = true;

            // Wait for a few seconds to verify that no reconnection attempts are being made
            await Assert.ThrowsAsync<TimeoutException>(async () => await reconnectOccurredAfterTestDisconnectedTheClient.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task MqttSessionClient_ConnectAsyncAbandonsRetryIfUserDisconnects()
        {
            using MockMqttClient mockClient = new MockMqttClient();

            mockClient.OnConnectAttempt += (args) =>
            {
                throw new MqttCommunicationTimedOutException();
            };

            await using MqttSessionClient sessionClient = new(mockClient, new MqttSessionClientOptions() { RetryOnFirstConnect = true });

            // This task should never return successfully since the mock MQTT client always throws when connecting.
            // The task should complete with an exception once this test calls to disconnect the session client, though.
            Task<MqttClientConnectResult> connAckTask = sessionClient.ConnectAsync(GetClientOptions());
            await sessionClient.DisconnectAsync();

            await Assert.ThrowsAsync<MqttCommunicationTimedOutException>(async () => await connAckTask.WaitAsync(TimeSpan.FromSeconds(30)));
        }

        [Fact]
        public async Task MqttSessionClient_ConnectAsyncAbandonsRetryIfUserCancelsIt()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            using CancellationTokenSource cts = new();
            int connectAttemptCount = 0;
            mockClient.OnConnectAttempt += (args) =>
            {
                connectAttemptCount++;

                // The first attempt simulates MQTTnet cancelling the connect attempt (which the session
                // client should retry). The second attempt simulates the user requesting cancellation of
                // this connect operation.
                if (connectAttemptCount == 2)
                {
                    cts.Cancel();
                }

                throw new OperationCanceledException();
            };

            await using MqttSessionClient sessionClient = new(mockClient, new MqttSessionClientOptions() { RetryOnFirstConnect = true });

            // The session client can treat OperationCanceledExceptions as retryable when the user-supplied
            // cancellation token is not cancelled, but it should treat that same exception as fatal if the user-supplied
            // token has requested cancellation.
            Task<MqttClientConnectResult> connAckTask = sessionClient.ConnectAsync(GetClientOptions(), cts.Token);

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await connAckTask.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.Equal(2, connectAttemptCount);
        }

        [Fact]
        public async Task MqttSessionClient_ConnectCallsConnect()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            var connectionSettings = new MqttConnectionSettings("someHostname")
            {
                KeepAlive = TimeSpan.FromSeconds(5),
                CleanStart = true,
                ClientId = Guid.NewGuid().ToString(),
                SessionExpiry = TimeSpan.FromSeconds(50),
            };

            MqttClientOptions expectedOptions = new MqttClientOptions(connectionSettings);

            await using MqttSessionClient sessionClient = new(mockClient);

            try
            {
                mockClient.OnConnectAttempt += (actualOptions) =>
                {
                    return Task.FromResult(mockClient.CompareExpectedConnectWithActual(expectedOptions, actualOptions, false));
                };

                MqttClientConnectResult connectResult = await sessionClient.ConnectAsync(expectedOptions);
                Assert.False(connectResult.IsSessionPresent);
                Assert.Equal(MqttClientConnectResultCode.Success, connectResult.ResultCode);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_DisconnectCallsDisconnect()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            var connectionSettings = new MqttConnectionSettings("someHostname");

            MqttClientDisconnectOptions expectedOptions = new MqttClientDisconnectOptions()
            {
                Reason = MqttClientDisconnectOptionsReason.ProtocolError,
                ReasonString = "some reason",
                SessionExpiryInterval = 0,
            };

            expectedOptions.AddUserProperty("someUserPropertyKey", "someUserPropertyValue");

            await using MqttSessionClient sessionClient = new(mockClient);

            mockClient.OnDisconnectAttempt += (actualOptions) =>
            {
                MockMqttClient.CompareExpectedDisconnectWithActual(expectedOptions, actualOptions);
                return Task.CompletedTask;
            };

            await sessionClient.DisconnectAsync(expectedOptions);
        }

        [Fact]
        public async Task MqttSessionClient_PublishCallsPublish()
        {
            using MockMqttClient mockClient = new MockMqttClient();

            await using MqttSessionClient sessionClient = new(mockClient);

            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                MqttApplicationMessage expectedMessage = new MqttApplicationMessage("some/topic", MqttQualityOfServiceLevel.AtMostOnce)
                {
                    PayloadSegment = new byte[] { 1, 2, 3 },
                    ContentType = "some content type",
                    CorrelationData = new byte[] { 3, 4, 5 },
                    MessageExpiryInterval = 12,
                    PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                    ResponseTopic = "some/response/topic",
                    Retain = true,
                    SubscriptionIdentifiers = new() { 34 },
                    TopicAlias = 38,
                };

                expectedMessage.AddUserProperty("someUserPropertyKey", "someUserPropertyValue");

                mockClient.OnPublishAttempt += (actualPublish) =>
                {
                    return Task.FromResult(MockMqttClient.CompareExpectedPublishWithActual(expectedMessage, actualPublish));
                };

                MqttClientPublishResult publishResult = await sessionClient.PublishAsync(expectedMessage);

                Assert.True(publishResult.IsSuccess, publishResult.ReasonString);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_SubscribeCallsSubscribe()
        {
            using MockMqttClient mockClient = new MockMqttClient();

            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                MqttClientSubscribeOptions expectedSubscribe =
                    new MqttClientSubscribeOptions(
                        new MqttTopicFilter("some/enqueued/topic/filter", MqttQualityOfServiceLevel.AtLeastOnce)
                        {
                            RetainAsPublished = true,
                            NoLocal = false,
                            RetainHandling = MqttRetainHandling.DoNotSendOnSubscribe,
                        })
                    {
                        SubscriptionIdentifier = 44,
                    };

                expectedSubscribe.AddUserProperty("someUserPropertyKey", "someUserPropertyValue");

                mockClient.OnSubscribeAttempt += (actualSubscribe) =>
                {
                    return Task.FromResult(MockMqttClient.CompareExpectedSubscribeWithActual(expectedSubscribe, actualSubscribe));
                };

                await sessionClient.SubscribeAsync(expectedSubscribe);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_UnsubscribeCallsUnsubscribe()
        {
            using MockMqttClient mockClient = new MockMqttClient();

            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                MqttClientUnsubscribeOptions expectedUnsubscribe = new MqttClientUnsubscribeOptions("some/enqueued/topic/filter");
                expectedUnsubscribe.AddUserProperty("someUserPropertyKey", "someUserPropertyValue");

                mockClient.OnUnsubscribeAttempt += (actualUnsubscribe) =>
                {
                    return Task.FromResult(MockMqttClient.CompareExpectedUnsubscribeWithActual(expectedUnsubscribe, actualUnsubscribe));
                };

                await sessionClient.UnsubscribeAsync(expectedUnsubscribe);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_PublishRetriesIfMqttCommunicationExceptionThrown()
        {
            await MqttSessionClient_PublishRetriesIfExceptionThrown(new MqttCommunicationException(new Exception()));
        }

        [Fact]
        public async Task MqttSessionClient_PublishRetriesIfIOExceptionThrown()
        {
            await MqttSessionClient_PublishRetriesIfExceptionThrown(new IOException());
        }

        private async Task MqttSessionClient_PublishRetriesIfExceptionThrown(Exception retriableException)
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                MqttApplicationMessage expectedMessage = new MqttApplicationMessage("some/topic")
                {
                    PayloadSegment = new byte[] { 1, 2, 3 },
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
                    CorrelationData = new byte[] { 3, 4, 5 },
                };

                int attempt = 0;
                mockClient.OnPublishAttempt += (actualPublish) =>
                {
                    attempt++;

                    if (attempt == 1)
                    {
                        _ = mockClient.SimulateServerInitiatedDisconnectAsync(retriableException);
                        throw new MQTTnet.Exceptions.MqttCommunicationException("Failed to deliver the publish for some reason");
                    }
                    else
                    {
                        return Task.FromResult(MockMqttClient.CompareExpectedPublishWithActual(expectedMessage, actualPublish));
                    }
                };

                MqttClientPublishResult publishResult =
                    await sessionClient.PublishAsync(expectedMessage).WaitAsync(TimeSpan.FromSeconds(30));
                Assert.True(publishResult.IsSuccess, publishResult.ReasonString);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_SubscribeRetriesIfMqttCommunicationExceptionThrown()
        {
            await MqttSessionClient_SubscribeRetriesIfExceptionThrown(new MqttCommunicationException(new Exception()));
        }

        [Fact]
        public async Task MqttSessionClient_SubscribeRetriesIfIOExceptionThrown()
        {
            await MqttSessionClient_SubscribeRetriesIfExceptionThrown(new IOException());
        }

        private async Task MqttSessionClient_SubscribeRetriesIfExceptionThrown(Exception retriableException)
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                MqttClientSubscribeOptions expectedSubscribe = new MqttClientSubscribeOptions("some/topic", MqttQualityOfServiceLevel.AtLeastOnce);

                int attempt = 0;
                mockClient.OnSubscribeAttempt += async (actualSubscribe) =>
                {
                    attempt++;

                    if (attempt == 1)
                    {
                        await mockClient.SimulateServerInitiatedDisconnectAsync(retriableException);
                        throw new MQTTnet.Exceptions.MqttCommunicationException("Failed to deliver the subscribe for some reason");
                    }
                    else
                    {
                        return MockMqttClient.CompareExpectedSubscribeWithActual(expectedSubscribe, actualSubscribe);
                    }
                };

                MqttClientSubscribeResult subscribeResult =
                    await sessionClient.SubscribeAsync(expectedSubscribe).WaitAsync(TimeSpan.FromSeconds(30));

                Assert.True(subscribeResult.IsSubAckSuccessful(MqttQualityOfServiceLevel.AtLeastOnce));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_UnsubscribeRetriesIfMqttCommunicationExceptionThrown()
        {
            await MqttSessionClient_UnsubscribeRetriesIfExceptionThrown(new MqttCommunicationException(new Exception()));
        }

        [Fact]
        public async Task MqttSessionClient_UnsubscribeRetriesIfIOExceptionThrown()
        {
            await MqttSessionClient_UnsubscribeRetriesIfExceptionThrown(new IOException());
        }

        private async Task MqttSessionClient_UnsubscribeRetriesIfExceptionThrown(Exception retriableException)
        {
            using MockMqttClient mockClient = new MockMqttClient();

            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                MqttClientUnsubscribeOptions expectedUnsubscribe = new MqttClientUnsubscribeOptions("some/topic");

                int attempt = 0;
                mockClient.OnUnsubscribeAttempt += async (actualUnsubscribe) =>
                {
                    attempt++;

                    if (attempt == 1)
                    {
                        await mockClient.SimulateServerInitiatedDisconnectAsync(retriableException);
                        throw new MQTTnet.Exceptions.MqttCommunicationException("Failed to deliver the unsubscribe for some reason");
                    }
                    else
                    {
                        return MockMqttClient.CompareExpectedUnsubscribeWithActual(expectedUnsubscribe, actualUnsubscribe);
                    }
                };

                await sessionClient.UnsubscribeAsync(expectedUnsubscribe).WaitAsync(TimeSpan.FromSeconds(30));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_AcknowledgeCallsAcknowledge()
        {
            using MockMqttClient mockClient = new MockMqttClient();

            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                TaskCompletionSource<MqttApplicationMessageReceivedEventArgs> messageReceivedTcs = new();
                sessionClient.ApplicationMessageReceivedAsync += (args) =>
                {
                    args.AutoAcknowledge = false;
                    messageReceivedTcs.TrySetResult(args);
                    return Task.CompletedTask;
                };

                await sessionClient.ConnectAsync(GetClientOptions());

                string expectedTopic = "some/topic";
                MqttClientSubscribeOptions expectedSubscribe = new MqttClientSubscribeOptions(expectedTopic);

                await sessionClient.SubscribeAsync(expectedSubscribe);

                MQTTnet.MqttApplicationMessage message = new MQTTnet.MqttApplicationMessageBuilder()
                    .WithTopic(expectedTopic)
                    .WithCorrelationData(Guid.NewGuid().ToByteArray())
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                await mockClient.SimulateNewMessageAsync(message);

                MqttApplicationMessageReceivedEventArgs msg = await messageReceivedTcs.Task;

                // Acknowledge only the second received message
                await msg.AcknowledgeAsync(CancellationToken.None);

                // While the session client should not send an ack yet since the application hasn't ack'd msg1, wait a bit before
                // checking what messages have/have not been acknowledged according to the mock MQTT client.
                await Task.Delay(TimeSpan.FromSeconds(2));

                Assert.Single(mockClient.AcknowledgedMessages);
                Assert.Equal(message.CorrelationData, mockClient.AcknowledgedMessages[0].ApplicationMessage.CorrelationData);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_PubackQueueClearedIfConnectionLost_MqttCommunicationException()
        {
            await MqttSessionClient_PubackQueueClearedIfConnectionLost(new MqttCommunicationException(new Exception()));
        }

        [Fact]
        public async Task MqttSessionClient_PubackQueueClearedIfConnectionLost_IOException()
        {
            await MqttSessionClient_PubackQueueClearedIfConnectionLost(new IOException());
        }

        private async Task MqttSessionClient_PubackQueueClearedIfConnectionLost(Exception retriableException)
        {
            using MockMqttClient mockClient = new MockMqttClient();

            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                TaskCompletionSource<MqttApplicationMessageReceivedEventArgs> messageReceivedTcs = new();
                sessionClient.ApplicationMessageReceivedAsync += async (args) =>
                {
                    args.AutoAcknowledge = false;
                    await mockClient.SimulateServerInitiatedDisconnectAsync(retriableException);
                    messageReceivedTcs.TrySetResult(args);
                };

                await sessionClient.ConnectAsync(GetClientOptions());

                string expectedTopic = "some/topic";
                MqttClientSubscribeOptions expectedSubscribe = new MqttClientSubscribeOptions(expectedTopic);

                await sessionClient.SubscribeAsync(expectedSubscribe);

                TaskCompletionSource OnReconnectComplete = new();
                mockClient.OnConnectAttempt += (actualOptions) =>
                {
                    _ = Task.Run(() =>
                    {
                        Task.Delay(TimeSpan.FromSeconds(1));
                        OnReconnectComplete.TrySetResult();
                    });

                    return Task.FromResult(new MQTTnet.MqttClientConnectResult());
                };

                MQTTnet.MqttApplicationMessage message = new MQTTnet.MqttApplicationMessageBuilder()
                    .WithTopic(expectedTopic)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                await mockClient.SimulateNewMessageAsync(message);

                try
                {
                    await OnReconnectComplete.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("Connection was expected to drop, but it never did");
                }

                MqttApplicationMessageReceivedEventArgs msg = await messageReceivedTcs.Task;

                // When ack'ing a QoS 1+ message from the session client, this function will typically return prior to the
                // acknowledgement being sent
                await msg.AcknowledgeAsync(CancellationToken.None);

                // Because of the above, wait a bit before checking what messages have/have not been acknowledged according to the mock
                // MQTT client.
                await Task.Delay(TimeSpan.FromSeconds(3));

                Assert.Empty(mockClient.AcknowledgedMessages);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_PubacksAreOrdered()
        {
            using MockMqttClient mockClient = new MockMqttClient();

            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                TaskCompletionSource<MqttApplicationMessageReceivedEventArgs> message1ReceivedTcs = new();
                TaskCompletionSource<MqttApplicationMessageReceivedEventArgs> message2ReceivedTcs = new();
                int messageReceivedCount = 0;
                sessionClient.ApplicationMessageReceivedAsync += (args) =>
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

                await sessionClient.ConnectAsync(GetClientOptions());

                string expectedTopic = "some/topic";
                MqttClientSubscribeOptions expectedSubscribe = new MqttClientSubscribeOptions(expectedTopic);

                await sessionClient.SubscribeAsync(expectedSubscribe);

                TaskCompletionSource OnReconnectComplete = new();
                mockClient.OnConnectAttempt += (actualOptions) =>
                {
                    _ = Task.Run(() =>
                    {
                        Task.Delay(TimeSpan.FromSeconds(1));
                        OnReconnectComplete.TrySetResult();
                    });

                    return Task.FromResult(new MQTTnet.MqttClientConnectResult());
                };

                MQTTnet.MqttApplicationMessage message1 = new MQTTnet.MqttApplicationMessageBuilder()
                    .WithTopic(expectedTopic)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithCorrelationData(Guid.NewGuid().ToByteArray())
                    .Build();
                MQTTnet.MqttApplicationMessage message2 = new MQTTnet.MqttApplicationMessageBuilder()
                    .WithTopic(expectedTopic)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithCorrelationData(Guid.NewGuid().ToByteArray())
                    .Build();

                await mockClient.SimulateNewMessageAsync(message1);
                await mockClient.SimulateNewMessageAsync(message2);

                MqttApplicationMessageReceivedEventArgs msg1 = await message1ReceivedTcs.Task;
                MqttApplicationMessageReceivedEventArgs msg2 = await message2ReceivedTcs.Task;

                // Acknowledge only the second received message
                await msg2.AcknowledgeAsync(CancellationToken.None);

                // While the session client should not send an ack yet since the application hasn't ack'd msg1, wait a bit before
                // checking what messages have/have not been acknowledged according to the mock MQTT client.
                await Task.Delay(TimeSpan.FromSeconds(2));

                Assert.Empty(mockClient.AcknowledgedMessages);

                // When ack'ing a QoS 1+ message from the session client, this function will typically return prior to the
                // acknowledgement being sent
                await msg1.AcknowledgeAsync(CancellationToken.None);

                // Because of the above, wait a bit before checking what messages have/have not been acknowledged according to the mock
                // MQTT client.
                await Task.Delay(TimeSpan.FromSeconds(2));

                // Now that both messages were acknowledged from the application layer, the session client should have sent both
                // acknowledgements in the same order that they were received in.
                Assert.Equal(2, mockClient.AcknowledgedMessages.Count);
                Assert.Equal(message1.CorrelationData, mockClient.AcknowledgedMessages[0].ApplicationMessage.CorrelationData);
                Assert.Equal(message2.CorrelationData, mockClient.AcknowledgedMessages[1].ApplicationMessage.CorrelationData);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_PublishQueueOverflow_DropNewMessage()
        {
            using MockMqttClient mockClient = new MockMqttClient();

            uint maxQueueSize = 1;
            MqttSessionClientOptions options = new MqttSessionClientOptions()
            {
                MaxPendingMessages = maxQueueSize,
                PendingMessagesOverflowStrategy = MqttPendingMessagesOverflowStrategy.DropNewMessage,
            };

            await using MqttSessionClient sessionClient = new(mockClient, options);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                mockClient.OnPublishAttempt += async (args) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    return new MQTTnet.MqttClientPublishResult(0, MQTTnet.MqttClientPublishReasonCode.Success, string.Empty, null);
                };

                MqttApplicationMessage msg = new MqttApplicationMessage("some/topic");

                _ = sessionClient.PublishAsync(msg);
                await Assert.ThrowsAsync<MessagePurgedFromQueueException>(async () => await sessionClient.PublishAsync(msg).WaitAsync(TimeSpan.FromSeconds(10)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_PublishQueueOverflow_DropOldMessage()
        {
            using MockMqttClient mockClient = new MockMqttClient();

            uint maxQueueSize = 1;
            MqttSessionClientOptions options = new MqttSessionClientOptions()
            {
                MaxPendingMessages = maxQueueSize,
                PendingMessagesOverflowStrategy = MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage,
            };

            await using MqttSessionClient sessionClient = new(mockClient, options);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                mockClient.OnPublishAttempt += async (args) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    return new MQTTnet.MqttClientPublishResult(0, MQTTnet.MqttClientPublishReasonCode.Success, string.Empty, null);
                };

                MqttApplicationMessage msg = new MqttApplicationMessage("some/topic");

                Task<MqttClientPublishResult> firstQueued = sessionClient.PublishAsync(msg);
                _ = sessionClient.PublishAsync(msg);
                await Assert.ThrowsAsync<MessagePurgedFromQueueException>(async () => await firstQueued.WaitAsync(TimeSpan.FromSeconds(10)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_SubscribeQueueOverflow_DropNewMessage()
        {
            using MockMqttClient mockClient = new MockMqttClient();

            uint maxQueueSize = 1;
            MqttSessionClientOptions options = new MqttSessionClientOptions()
            {
                MaxPendingMessages = maxQueueSize,
                PendingMessagesOverflowStrategy = MqttPendingMessagesOverflowStrategy.DropNewMessage,
            };

            await using MqttSessionClient sessionClient = new(mockClient, options);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                mockClient.OnSubscribeAttempt += async (args) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));

                    // This isn't a valid returned SUBACK, but the test doesn't need it to be since this isn't checked
                    return new MQTTnet.MqttClientSubscribeResult(0, new List<MQTTnet.MqttClientSubscribeResultItem>(), "", new List<MQTTnet.Packets.MqttUserProperty>());
                };

                MqttClientSubscribeOptions subscribe = new MqttClientSubscribeOptions("some/topic");

                _ = sessionClient.SubscribeAsync(subscribe);
                await Assert.ThrowsAsync<MessagePurgedFromQueueException>(async () => await sessionClient.SubscribeAsync(subscribe).WaitAsync(TimeSpan.FromSeconds(10)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_SubscribeQueueOverflow_DropOldMessage()
        {
            using MockMqttClient mockClient = new MockMqttClient();

            uint maxQueueSize = 1;
            MqttSessionClientOptions options = new MqttSessionClientOptions()
            {
                MaxPendingMessages = maxQueueSize,
                PendingMessagesOverflowStrategy = MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage,
            };

            await using MqttSessionClient sessionClient = new(mockClient, options);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                mockClient.OnSubscribeAttempt += async (args) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));

                    // This isn't a valid returned SUBACK, but the test doesn't need it to be since this isn't checked
                    return new MQTTnet.MqttClientSubscribeResult(0, new List<MQTTnet.MqttClientSubscribeResultItem>(), "", new List<MQTTnet.Packets.MqttUserProperty>());
                };

                MqttClientSubscribeOptions subscribe = new MqttClientSubscribeOptions("some/topic");

                Task<MqttClientSubscribeResult> firstQueued = sessionClient.SubscribeAsync(subscribe);
                _ = sessionClient.SubscribeAsync(subscribe);
                await Assert.ThrowsAsync<MessagePurgedFromQueueException>(async () => await firstQueued.WaitAsync(TimeSpan.FromSeconds(10)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_UnsubscribeQueueOverflow_DropNewMessage()
        {
            using MockMqttClient mockClient = new MockMqttClient();

            uint maxQueueSize = 1;
            MqttSessionClientOptions options = new MqttSessionClientOptions()
            {
                MaxPendingMessages = maxQueueSize,
                PendingMessagesOverflowStrategy = MqttPendingMessagesOverflowStrategy.DropNewMessage,
            };

            await using MqttSessionClient sessionClient = new(mockClient, options);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                mockClient.OnUnsubscribeAttempt += async (args) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));

                    // This isn't a valid returned UNSUBACK, but the test doesn't need it to be since this isn't checked
                    return new MQTTnet.MqttClientUnsubscribeResult(0, new List<MQTTnet.MqttClientUnsubscribeResultItem>(), "", new List<MQTTnet.Packets.MqttUserProperty>());
                };

                MqttClientUnsubscribeOptions unsubscribe = new MqttClientUnsubscribeOptions("some/topic");

                _ = sessionClient.UnsubscribeAsync(unsubscribe);
                await Assert.ThrowsAsync<MessagePurgedFromQueueException>(async () => await sessionClient.UnsubscribeAsync(unsubscribe).WaitAsync(TimeSpan.FromSeconds(10)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_UnsubscribeQueueOverflow_DropOldMessage()
        {
            using MockMqttClient mockClient = new MockMqttClient();

            uint maxQueueSize = 1;
            MqttSessionClientOptions options = new MqttSessionClientOptions()
            {
                MaxPendingMessages = maxQueueSize,
                PendingMessagesOverflowStrategy = MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage,
            };

            await using MqttSessionClient sessionClient = new(mockClient, options);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                mockClient.OnUnsubscribeAttempt += async (args) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));

                    // This isn't a valid returned UNSUBACK, but the test doesn't need it to be since this isn't checked
                    return new MQTTnet.MqttClientUnsubscribeResult(0, new List<MQTTnet.MqttClientUnsubscribeResultItem>(), "", new List<MQTTnet.Packets.MqttUserProperty>());
                };

                MqttClientUnsubscribeOptions unsubscribe = new MqttClientUnsubscribeOptions("some/topic");

                Task<MqttClientUnsubscribeResult> firstQueued = sessionClient.UnsubscribeAsync(unsubscribe);
                _ = sessionClient.UnsubscribeAsync(unsubscribe);
                await Assert.ThrowsAsync<MessagePurgedFromQueueException>(async () => await firstQueued.WaitAsync(TimeSpan.FromSeconds(10)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_PublishReportsFatalException()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                MqttProtocolViolationException expectedException = new MqttProtocolViolationException("Some fake exception");
                mockClient.OnPublishAttempt += (args) =>
                {
                    throw expectedException;
                };

                MqttApplicationMessage msg = new MqttApplicationMessage("some/topic");

                await Assert.ThrowsAsync<MqttProtocolViolationException>(async () => await sessionClient.PublishAsync(msg).WaitAsync(TimeSpan.FromSeconds(30)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_SubscribeReportsFatalException()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                MqttProtocolViolationException expectedException = new MqttProtocolViolationException("Some fake exception");
                mockClient.OnSubscribeAttempt += (args) =>
                {
                    throw expectedException;
                };

                MqttClientSubscribeOptions subscribe = new MqttClientSubscribeOptions("some/topic");

                await Assert.ThrowsAsync<MqttProtocolViolationException>(async () => await sessionClient.SubscribeAsync(subscribe).WaitAsync(TimeSpan.FromSeconds(30)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_UnsubscribeReportsFatalException()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                MqttProtocolViolationException expectedException = new MqttProtocolViolationException("Some fake exception");
                mockClient.OnUnsubscribeAttempt += (args) =>
                {
                    throw expectedException;
                };

                MqttClientUnsubscribeOptions unsubscribe = new MqttClientUnsubscribeOptions("some/topic");

                await Assert.ThrowsAsync<MqttProtocolViolationException>(async () => await sessionClient.UnsubscribeAsync(unsubscribe).WaitAsync(TimeSpan.FromSeconds(30)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Theory]
        [ClassData(typeof(UnsuccessfulAndUnretriablePubackCodesClassData))]
        public async Task MqttSessionClient_PublishReportsNonSuccessfulPubackResultCode(MQTTnet.MqttClientPublishReasonCode reasonCode)
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                int attemptNumber = 0;
                mockClient.OnPublishAttempt += (args) =>
                {
                    // The client should not actually retry here, so this logic should help catch if it does retry.
                    attemptNumber++;
                    if (attemptNumber > 1)
                    {
                        return Task.FromResult(new MQTTnet.MqttClientPublishResult(0, MQTTnet.MqttClientPublishReasonCode.Success, "", new List<MQTTnet.Packets.MqttUserProperty>()));
                    }
                    else
                    {
                        return Task.FromResult(new MQTTnet.MqttClientPublishResult(0, reasonCode, "", new List<MQTTnet.Packets.MqttUserProperty>()));
                    }
                };

                MqttApplicationMessage msg = new MqttApplicationMessage("some/topic");

                MqttClientPublishResult result = await sessionClient.PublishAsync(msg).WaitAsync(TimeSpan.FromSeconds(30));
                Assert.Equal(reasonCode, (MQTTnet.MqttClientPublishReasonCode)result.ReasonCode);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Theory]
        [ClassData(typeof(UnsuccessfulAndUnretriableSubackCodesClassData))]
        public async Task MqttSessionClient_SubscribeReportsNonSuccessfulSubackResultCode(MQTTnet.MqttClientSubscribeResultCode reasonCode)
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                int attemptNumber = 0;
                mockClient.OnSubscribeAttempt += (args) =>
                {
                    // The client should not actually retry here, so this logic should help catch if it does retry.
                    attemptNumber++;
                    var subscribeResultItems = new List<MQTTnet.MqttClientSubscribeResultItem>();

                    if (attemptNumber > 1)
                    {
                        subscribeResultItems.Add(
                            new MQTTnet.MqttClientSubscribeResultItem(
                                new MQTTnet.MqttTopicFilterBuilder()
                                    .WithTopic(args.TopicFilters.First().Topic)
                                    .WithQualityOfServiceLevel(args.TopicFilters.First().QualityOfServiceLevel)
                                    .Build(),
                                MQTTnet.MqttClientSubscribeResultCode.GrantedQoS1));
                    }
                    else
                    {
                        subscribeResultItems.Add(
                            new MQTTnet.MqttClientSubscribeResultItem(
                                new MQTTnet.MqttTopicFilterBuilder()
                                    .WithTopic(args.TopicFilters.First().Topic)
                                    .WithQualityOfServiceLevel(args.TopicFilters.First().QualityOfServiceLevel)
                                    .Build(),
                                reasonCode));
                    }

                    return Task.FromResult(new MQTTnet.MqttClientSubscribeResult(0, subscribeResultItems, "", new List<MQTTnet.Packets.MqttUserProperty>()));
                };

                MqttClientSubscribeOptions subscribe = new MqttClientSubscribeOptions("some/Topic", MqttQualityOfServiceLevel.AtLeastOnce);

                MqttClientSubscribeResult result = await sessionClient.SubscribeAsync(subscribe).WaitAsync(TimeSpan.FromSeconds(30));
                Assert.Single(result.Items);
                Assert.Equal(reasonCode, (MQTTnet.MqttClientSubscribeResultCode)result.Items.First().ReasonCode);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Theory]
        [ClassData(typeof(UnsuccessfulAndUnretriableUnsubackCodesClassData))]
        public async Task MqttSessionClient_UnubscribeReportsNonSuccessfulUnsubackResultCode(MQTTnet.MqttClientUnsubscribeResultCode reasonCode)
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                int attemptNumber = 0;
                mockClient.OnUnsubscribeAttempt += (args) =>
                {
                    // The client should not actually retry here, so this logic should help catch if it does retry.
                    attemptNumber++;
                    var unsubscribeResultItems = new List<MQTTnet.MqttClientUnsubscribeResultItem>();

                    if (attemptNumber > 1)
                    {
                        unsubscribeResultItems.Add(
                            new MQTTnet.MqttClientUnsubscribeResultItem(args.TopicFilters.First(), MQTTnet.MqttClientUnsubscribeResultCode.Success));
                    }
                    else
                    {
                        unsubscribeResultItems.Add(
                            new MQTTnet.MqttClientUnsubscribeResultItem(args.TopicFilters.First(), reasonCode));
                    }

                    return Task.FromResult(new MQTTnet.MqttClientUnsubscribeResult(0, unsubscribeResultItems, "", new List<MQTTnet.Packets.MqttUserProperty>()));
                };

                MqttClientUnsubscribeOptions unsubscribe = new MqttClientUnsubscribeOptions("some/Topic");

                MqttClientUnsubscribeResult result = await sessionClient.UnsubscribeAsync(unsubscribe).WaitAsync(TimeSpan.FromSeconds(30));
                Assert.Single(result.Items);
                Assert.Equal(reasonCode, (MQTTnet.MqttClientUnsubscribeResultCode)result.Items.First().ReasonCode);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_ClientReportsSessionLossUponReconnection()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                TaskCompletionSource<MqttClientDisconnectedEventArgs> sessionLostArgsTcs = new();
                sessionClient.SessionLostAsync += (args) =>
                {
                    sessionLostArgsTcs.TrySetResult(args);
                    return Task.CompletedTask;
                };

                TaskCompletionSource disconnectTcs = new();
                mockClient.OnDisconnectAttempt += (args) =>
                {
                    disconnectTcs.TrySetResult();
                    return Task.CompletedTask;
                };

                mockClient.OnConnectAttempt += (args) =>
                {
                    // The next connect attempt will succeed, but the session will no longer be present on the broker side
                    return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulInitialConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500));
                };

                await mockClient.SimulateServerInitiatedDisconnectAsync(new MqttCommunicationException("some disconnect"));

                MqttClientDisconnectedEventArgs? sessionLostEventArgs = null;
                try
                {
                    sessionLostEventArgs = await sessionLostArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    await disconnectTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("Timed out waiting for the session client to notify the application that the session was lost");
                }

                Assert.NotNull(sessionLostEventArgs);
                Assert.NotNull(sessionLostEventArgs.Exception);
                Assert.True(sessionLostEventArgs.Exception is MqttSessionExpiredException);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_ClientReportsRetryPolicyExhaustedWhileReconnecting()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            int maxReconnectTryCount = 3;
            TestRetryPolicy testRetryPolicy = new(maxReconnectTryCount);
            MqttSessionClientOptions options = new MqttSessionClientOptions()
            {
                ConnectionRetryPolicy = testRetryPolicy,
            };

            await using MqttSessionClient sessionClient = new(mockClient, options);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                int connectAttemptsMade = 0;
                mockClient.OnConnectAttempt += (args) =>
                {
                    connectAttemptsMade++;
                    // All reconnect attempts will result in an unsuccessful CONNACK so that the retry policy eventually ends
                    return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.UnsuccessfulReconnectConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500));
                };

                TaskCompletionSource<MqttClientDisconnectedEventArgs> disconnectArgsTcs = new();
                sessionClient.SessionLostAsync += (args) =>
                {
                    disconnectArgsTcs.TrySetResult(args);
                    return Task.CompletedTask;
                };

                await mockClient.SimulateServerInitiatedDisconnectAsync(new MqttCommunicationException("some disconnect"));

                MqttClientDisconnectedEventArgs? disconnectedEventArgs = null;
                try
                {
                    disconnectedEventArgs = await disconnectArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("Timed out waiting for the session client to notify the application that reconnection was abandoned");
                }

                Assert.NotNull(disconnectedEventArgs);
                Assert.NotNull(disconnectedEventArgs.Exception);
                Assert.True(disconnectedEventArgs.Exception is RetryExpiredException);

                // The retry policy should be consulted [max] + 1 times (where the max + 1th time it says to stop retrying)
                Assert.Equal(maxReconnectTryCount + 1, testRetryPolicy.CurrentRetryCount);

                // The mqtt client should only attempt to connect [max] times
                Assert.Equal(maxReconnectTryCount, connectAttemptsMade);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_PublishChecksCancellationToken()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                using CancellationTokenSource cts = new CancellationTokenSource();
                cts.Cancel();

                var msg = new MqttApplicationMessage("some/topic");

                await Assert.ThrowsAsync<OperationCanceledException>(
                    async () => await sessionClient.PublishAsync(msg, cts.Token).WaitAsync(TimeSpan.FromSeconds(30)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_SubscribeChecksCancellationToken()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                using CancellationTokenSource cts = new CancellationTokenSource();
                cts.Cancel();

                var subscribeOptions = new MqttClientSubscribeOptions("some/topic", MqttQualityOfServiceLevel.AtLeastOnce);

                await Assert.ThrowsAsync<OperationCanceledException>(
                    async () => await sessionClient.SubscribeAsync(subscribeOptions, cts.Token).WaitAsync(TimeSpan.FromSeconds(30)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_UnsubscribeChecksCancellationToken()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                using CancellationTokenSource cts = new CancellationTokenSource();
                cts.Cancel();

                var unsubscribeOptions = new MqttClientUnsubscribeOptions("some/topic");

                await Assert.ThrowsAsync<OperationCanceledException>(
                    async () => await sessionClient.UnsubscribeAsync(unsubscribeOptions, cts.Token).WaitAsync(TimeSpan.FromSeconds(30)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_ConnectChecksCancellationToken()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                using CancellationTokenSource cts = new CancellationTokenSource();
                cts.Cancel();

                await Assert.ThrowsAsync<OperationCanceledException>(
                    async () => await sessionClient.ConnectAsync(GetClientOptions(), cts.Token));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_ConnectChecksCancellationTokenDuringSubsequentAttempts()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            TestRetryPolicy testRetryPolicy = new(int.MaxValue);
            MqttSessionClientOptions options = new MqttSessionClientOptions()
            {
                ConnectionRetryPolicy = testRetryPolicy,
                RetryOnFirstConnect = true,
            };

            await using MqttSessionClient sessionClient = new(mockClient, options);
            try
            {
                using CancellationTokenSource cts = new CancellationTokenSource();

                int connectAttemptCount = 0;
                mockClient.OnConnectAttempt += (args) =>
                {
                    connectAttemptCount++;
                    if (connectAttemptCount == 3)
                    {
                        cts.Cancel();
                    }

                    return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.UnsuccessfulReconnectConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500));
                };

                await Assert.ThrowsAsync<OperationCanceledException>(
                    async () => await sessionClient.ConnectAsync(GetClientOptions(), cts.Token));
                Assert.Equal(3, connectAttemptCount);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_DisconnectChecksCancellationToken()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                using CancellationTokenSource cts = new CancellationTokenSource();
                cts.Cancel();

                await Assert.ThrowsAsync<OperationCanceledException>(
                    async () => await sessionClient.DisconnectAsync(null, cts.Token));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClientCanChangeCredentialsDuringReconnection()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                X509Certificate2 mockCertificate1 = GenerateSelfSignedCertificate("cert1");
                X509Certificate2 mockCertificate2 = GenerateSelfSignedCertificate("cert2");

                X509Certificate? actualCertificate = null;
                TaskCompletionSource reconnectOccurred = new();
                int connectCount = 0;
                mockClient.OnConnectAttempt += (args) =>
                {
                    connectCount++;

                    var clientCerts = args.ChannelOptions.TlsOptions.ClientCertificatesProvider.GetCertificates();
                    actualCertificate = clientCerts[0];

                    if (connectCount > 1)
                    {
                        reconnectOccurred.TrySetResult();
                    }

                    return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulReconnectConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500));
                };

                var certificateProvider = new TestCertificateProvider(mockCertificate1);
                MqttClientOptions mqttClientOptions =
                    new MqttClientOptions(
                        new MqttClientTcpOptions("somehostname", 1883)
                        {
                            TlsOptions = new MqttClientTlsOptions()
                            {
                                ClientCertificatesProvider = certificateProvider,
                            }
                        })
                    {
                        SessionExpiryInterval = 10,
                    };

                await sessionClient.ConnectAsync(mqttClientOptions);

                Assert.NotNull(actualCertificate);
                Assert.Equal(mockCertificate1.Subject, actualCertificate.Subject);
                Assert.NotEqual(mockCertificate2.Subject, actualCertificate.Subject);

                // Change the credentials and then simulate a transient network drop. The session client should reconnect
                // sucessfully and use this new certificate.
                certificateProvider.CurrentCertificate = mockCertificate2;

                await mockClient.SimulateServerInitiatedDisconnectAsync(new MqttCommunicationException("some transient error"));

                await reconnectOccurred.Task.WaitAsync(TimeSpan.FromSeconds(30));

                Assert.NotNull(actualCertificate);
                Assert.Equal(mockCertificate2.Subject, actualCertificate.Subject);
                Assert.NotEqual(mockCertificate1.Subject, actualCertificate.Subject);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClientHandlesThrownExceptionsInPublishReceivedCallback()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockMqttClient);
            await sessionClient.ConnectAsync(GetClientOptions());

            try
            {
                sessionClient.ApplicationMessageReceivedAsync += (args) =>
                {
                    throw new Exception("Failed to process a received publish");
                };

                var expectedMessage = new MQTTnet.MqttApplicationMessageBuilder()
                        .WithTopic("some/topic")
                        .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)MqttQualityOfServiceLevel.AtLeastOnce)
                        .Build();

                // This method would throw if the session client didn't catch the thrown exception.
                await mockMqttClient.SimulateNewMessageAsync(expectedMessage, 12);

                // It may take a second for the acknowledgement thread on the client to actually acknowledge the publish even
                // with autoack
                await Task.Delay(TimeSpan.FromSeconds(1));

                Assert.Single(mockMqttClient.AcknowledgedMessages);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_CanCancelQueuedPublishWhenDisconnected()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);

            var msg = new MqttApplicationMessage("someTopic");
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            // Attempt to publish a message when the client isn't connected. The message should successfully
            // be enqueued locally, but it should never be sent. Provide a cancellation token as the way
            // to end the method call.
            //
            // Note that this method will either throw OperationCanceledException because the cancellation
            // token was properly checked, or it will throw a TimeoutException because the WaitAsync() call ended
            // it. The only success case is the OperationCanceledException, though. the WaitAsync() call
            // is just there to prevent this test from running indefinitely if the cancellation token flow
            // isn't working correctly.
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () =>
                    await sessionClient.PublishAsync(msg, cts.Token).WaitAsync(TimeSpan.FromSeconds(10)));
        }

        [Fact]
        public async Task MqttSessionClient_CanCancelQueuedSubscribeWhenDisconnected()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);

            var subscribe = new MqttClientSubscribeOptions("someTopic");
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            // Attempt to subscribe when the client isn't connected. The subscribe request should successfully
            // be enqueued locally, but it should never be sent. Provide a cancellation token as the way
            // to end the method call.
            //
            // Note that this method will either throw OperationCanceledException because the cancellation
            // token was properly checked, or it will throw a TimeoutException because the WaitAsync() call ended
            // it. The only success case is the OperationCanceledException, though. the WaitAsync() call
            // is just there to prevent this test from running indefinitely if the cancellation token flow
            // isn't working correctly.
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () =>
                    await sessionClient.SubscribeAsync(subscribe, cts.Token).WaitAsync(TimeSpan.FromSeconds(10)));
        }

        [Fact]
        public async Task MqttSessionClient_CanCancelQueuedUnsubscribeWhenDisconnected()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);

            var unsubscribe = new MqttClientUnsubscribeOptions("someTopic");
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            // Attempt to subscribe when the client isn't connected. The subscribe request should successfully
            // be enqueued locally, but it should never be sent. Provide a cancellation token as the way
            // to end the method call.
            //
            // Note that this method will either throw OperationCanceledException because the cancellation
            // token was properly checked, or it will throw a TimeoutException because the WaitAsync() call ended
            // it. The only success case is the OperationCanceledException, though. the WaitAsync() call
            // is just there to prevent this test from running indefinitely if the cancellation token flow
            // isn't working correctly.
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () =>
                    await sessionClient.UnsubscribeAsync(unsubscribe, cts.Token).WaitAsync(TimeSpan.FromSeconds(10)));
        }

        [Fact]
        public async Task MqttSessionClient_CanAccessAssignedClientId()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            string expectedClientId = "SomeBrokerAssignedClientId";
            mockClient.OnConnectAttempt += (args) =>
            {
                var packet = new MQTTnet.Packets.MqttConnAckPacket()
                {
                    ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.Success,
                    AssignedClientIdentifier = expectedClientId,
                };

                return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(packet, MQTTnet.Formatter.MqttProtocolVersion.V500));
            };

            try
            {
                // When connecting to an MQTT broker with an empty clientId, the broker should assign one to the client in the CONNACK.
                // This test ensures that the user of the session client can access that assigned client Id
                var options = new MqttClientOptions(new MqttClientTcpOptions("someHost", 1883))
                {
                    SessionExpiryInterval = 10,
                    ClientId = "",
                };

                await sessionClient.ConnectAsync(options);

                Assert.Equal(expectedClientId, sessionClient.ClientId);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_QueuedRequestsAreCancelledIfSessionIsLost_PublishAsync()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                TaskCompletionSource<MqttClientDisconnectedEventArgs> sessionLostArgsTcs = new();
                sessionClient.SessionLostAsync += (args) =>
                {
                    sessionLostArgsTcs.TrySetResult(args);
                    return Task.CompletedTask;
                };

                TaskCompletionSource<MqttClientDisconnectedEventArgs> disconnectedArgsTcs = new();
                sessionClient.DisconnectedAsync += (args) =>
                {
                    disconnectedArgsTcs.TrySetResult(args);
                    return Task.CompletedTask;
                };

                await sessionClient.ConnectAsync(GetClientOptions());

                TaskCompletionSource requestEnqueued = new();
                mockClient.OnConnectAttempt += async (args) =>
                {
                    // Wait until a the pub/sub/unsub request has been enqueued before allowing the reconnection
                    // to succeed but the session to be reported as lost

                    await requestEnqueued.Task;

                    // The next connect attempt will succeed, but the session will no longer be present on the broker side
                    return new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulInitialConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500);
                };

                await mockClient.SimulateServerInitiatedDisconnectAsync(new MqttCommunicationException("some disconnect"));

                MqttClientDisconnectedEventArgs? connectionLostEventArgs = null;
                try
                {
                    connectionLostEventArgs = await disconnectedArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("Timed out waiting for the session client to notify the application that the connection was lost");
                }

                // Now that the client is in a reconnecting state, enqueue the request
                var publishTask = sessionClient.PublishAsync(new MqttApplicationMessage("someTopic"));

                requestEnqueued.TrySetResult();

                MqttClientDisconnectedEventArgs? sessionLostEventArgs = null;
                try
                {
                    sessionLostEventArgs = await sessionLostArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("Timed out waiting for the session client to notify the application that the session was lost");
                }

                // Since the session was lost prior to this request being completed, the session client should notify the user
                // that the request failed because the session was lost.
                await Assert.ThrowsAsync<MqttSessionExpiredException>(async () => await publishTask.WaitAsync(TimeSpan.FromSeconds(30)));

                Assert.NotNull(sessionLostEventArgs);
                Assert.NotNull(sessionLostEventArgs.Exception);
                Assert.True(sessionLostEventArgs.Exception is MqttSessionExpiredException);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_QueuedRequestsAreCancelledIfSessionIsLost_SubscribeAsync()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                TaskCompletionSource<MqttClientDisconnectedEventArgs> sessionLostArgsTcs = new();
                sessionClient.SessionLostAsync += (args) =>
                {
                    sessionLostArgsTcs.TrySetResult(args);
                    return Task.CompletedTask;
                };

                TaskCompletionSource<MqttClientDisconnectedEventArgs> disconnectedArgsTcs = new();
                sessionClient.DisconnectedAsync += (args) =>
                {
                    disconnectedArgsTcs.TrySetResult(args);
                    return Task.CompletedTask;
                };

                await sessionClient.ConnectAsync(GetClientOptions());

                TaskCompletionSource requestEnqueued = new();
                mockClient.OnConnectAttempt += async (args) =>
                {
                    // Wait until a the pub/sub/unsub request has been enqueued before allowing the reconnection
                    // to succeed but the session to be reported as lost
                    await requestEnqueued.Task;

                    // The next connect attempt will succeed, but the session will no longer be present on the broker side
                    return new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulInitialConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500);
                };

                await mockClient.SimulateServerInitiatedDisconnectAsync(new MqttCommunicationException("some disconnect"));

                MqttClientDisconnectedEventArgs? connectionLostEventArgs = null;
                try
                {
                    connectionLostEventArgs = await disconnectedArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("Timed out waiting for the session client to notify the application that the connection was lost");
                }

                // Now that the client is in a reconnecting state, enqueue the request
                var subscribeTask = sessionClient.SubscribeAsync(new MqttClientSubscribeOptions("some/topic"));

                requestEnqueued.TrySetResult();

                MqttClientDisconnectedEventArgs? sessionLostEventArgs = null;
                try
                {
                    sessionLostEventArgs = await sessionLostArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("Timed out waiting for the session client to notify the application that the session was lost");
                }

                // Since the session was lost prior to this request being completed, the session client should notify the user
                // that the request failed because the session was lost.
                await Assert.ThrowsAsync<MqttSessionExpiredException>(async () => await subscribeTask.WaitAsync(TimeSpan.FromSeconds(30)));

                Assert.NotNull(sessionLostEventArgs);
                Assert.NotNull(sessionLostEventArgs.Exception);
                Assert.True(sessionLostEventArgs.Exception is MqttSessionExpiredException);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_QueuedRequestsAreCancelledIfSessionIsLost_UnsubscribeAsync()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                TaskCompletionSource<MqttClientDisconnectedEventArgs> sessionLostArgsTcs = new();
                sessionClient.SessionLostAsync += (args) =>
                {
                    sessionLostArgsTcs.TrySetResult(args);
                    return Task.CompletedTask;
                };

                TaskCompletionSource<MqttClientDisconnectedEventArgs> disconnectedArgsTcs = new();
                sessionClient.DisconnectedAsync += (args) =>
                {
                    disconnectedArgsTcs.TrySetResult(args);
                    return Task.CompletedTask;
                };

                await sessionClient.ConnectAsync(GetClientOptions());

                TaskCompletionSource requestEnqueued = new();
                mockClient.OnConnectAttempt += async (args) =>
                {
                    // Wait until a the pub/sub/unsub request has been enqueued before allowing the reconnection
                    // to succeed but the session to be reported as lost
                    await requestEnqueued.Task;

                    // The next connect attempt will succeed, but the session will no longer be present on the broker side
                    return new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulInitialConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500);
                };

                await mockClient.SimulateServerInitiatedDisconnectAsync(new MqttCommunicationException("some disconnect"));

                MqttClientDisconnectedEventArgs? connectionLostEventArgs = null;
                try
                {
                    connectionLostEventArgs = await disconnectedArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("Timed out waiting for the session client to notify the application that the connection was lost");
                }

                // Now that the client is in a reconnecting state, enqueue the request
                var unsubscribeTask = sessionClient.UnsubscribeAsync(new MqttClientUnsubscribeOptions("otherTopic"));

                requestEnqueued.TrySetResult();

                MqttClientDisconnectedEventArgs? sessionLostEventArgs = null;
                try
                {
                    sessionLostEventArgs = await sessionLostArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("Timed out waiting for the session client to notify the application that the session was lost");
                }

                // Since the session was lost prior to this request being completed, the session client should notify the user
                // that the request failed because the session was lost.
                await Assert.ThrowsAsync<MqttSessionExpiredException>(async () => await unsubscribeTask.WaitAsync(TimeSpan.FromSeconds(30)));

                Assert.NotNull(sessionLostEventArgs);
                Assert.NotNull(sessionLostEventArgs.Exception);
                Assert.True(sessionLostEventArgs.Exception is MqttSessionExpiredException);
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_QueuedRequestsAreCancelledIfClientIsClosed_PublishAsync()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                TaskCompletionSource<MqttClientDisconnectedEventArgs> sessionLostArgsTcs = new();
                sessionClient.SessionLostAsync += (args) =>
                {
                    sessionLostArgsTcs.TrySetResult(args);
                    return Task.CompletedTask;
                };

                TaskCompletionSource<MqttClientDisconnectedEventArgs> disconnectedArgsTcs = new();
                sessionClient.DisconnectedAsync += (args) =>
                {
                    disconnectedArgsTcs.TrySetResult(args);
                    return Task.CompletedTask;
                };

                await sessionClient.ConnectAsync(GetClientOptions());

                TaskCompletionSource requestEnqueued = new();

                mockClient.OnPublishAttempt += async (args) =>
                {
                    requestEnqueued.TrySetResult();
                    await Task.Delay(TimeSpan.FromHours(1));
                    throw new Exception("This code shouldn't return");
                };

                // Now that the client is in a reconnecting state, enqueue the request
                var publishTask = sessionClient.PublishAsync(new MqttApplicationMessage("sometopic"));

                await sessionClient.DisconnectAsync();

                // Since the session was lost prior to this request being completed, the session client should notify the user
                // that the request failed because the session was lost.
                await Assert.ThrowsAsync<MqttSessionExpiredException>(async () => await publishTask.WaitAsync(TimeSpan.FromSeconds(30)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_QueuedRequestsAreCancelledIfClientIsClosed_SubscribeAsync()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                TaskCompletionSource requestEnqueued = new();
                mockClient.OnSubscribeAttempt += async (args) =>
                {
                    requestEnqueued.TrySetResult();
                    await Task.Delay(TimeSpan.FromHours(1));
                    throw new Exception("This code shouldn't return");
                };

                // Now that the client is in a reconnecting state, enqueue the request
                var subscribeTask = sessionClient.SubscribeAsync(new MqttClientSubscribeOptions("some/topic"));

                await sessionClient.DisconnectAsync();

                // Since the session was lost prior to this request being completed, the session client should notify the user
                // that the request failed because the session was lost.
                await Assert.ThrowsAsync<MqttSessionExpiredException>(async () => await subscribeTask.WaitAsync(TimeSpan.FromSeconds(30)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_QueuedRequestsAreCancelledIfClientIsClosed_UnsubscribeAsync()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockClient);
            try
            {
                await sessionClient.ConnectAsync(GetClientOptions());

                TaskCompletionSource requestEnqueued = new();
                mockClient.OnUnsubscribeAttempt += async (args) =>
                {
                    requestEnqueued.TrySetResult();
                    await Task.Delay(TimeSpan.FromHours(1));
                    throw new Exception("This code shouldn't return");
                };

                var unsubscribeTask = sessionClient.UnsubscribeAsync(new MqttClientUnsubscribeOptions("otherTopic"));

                await sessionClient.DisconnectAsync();

                // Since the session was lost prior to this request being completed, the session client should notify the user
                // that the request failed because the session was lost.
                await Assert.ThrowsAsync<MqttSessionExpiredException>(async () => await unsubscribeTask.WaitAsync(TimeSpan.FromSeconds(30)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_QueuedRequestsAreCancelledIfRetryIsExhausted_PublishAsync()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            MqttSessionClientOptions options = new()
            {
                ConnectionRetryPolicy = new TestRetryPolicy(0),
            };
            await using MqttSessionClient sessionClient = new(mockClient, options);
            try
            {
                TaskCompletionSource<MqttClientDisconnectedEventArgs> sessionLostArgsTcs = new();
                sessionClient.SessionLostAsync += (args) =>
                {
                    sessionLostArgsTcs.TrySetResult(args);
                    return Task.CompletedTask;
                };

                TaskCompletionSource requestEnqueued = new();
                mockClient.OnPublishAttempt += async (args) =>
                {
                    requestEnqueued.TrySetResult();
                    await Task.Delay(TimeSpan.FromHours(1));
                    throw new Exception("This should never be reached");
                };

                await sessionClient.ConnectAsync(GetClientOptions());

                // This operation won't finish prior to the disconnection and should eventually
                // report that it was cancelled upon retry policy being exhausted
                var publishTask = sessionClient.PublishAsync(new MqttApplicationMessage("someTopic"));

                await mockClient.SimulateServerInitiatedDisconnectAsync(new MqttCommunicationException("some disconnect"));

                try
                {
                    await sessionLostArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("Timed out waiting for the session client to notify the application that the session was lost");
                }

                // Since the session was lost prior to this request being completed, the session client should notify the user
                // that the request failed because the session was lost.
                await Assert.ThrowsAsync<RetryExpiredException>(async () => await publishTask.WaitAsync(TimeSpan.FromSeconds(30)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_QueuedRequestsAreCancelledIfRetryIsExhausted_SubscribeAsync()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            MqttSessionClientOptions options = new()
            {
                ConnectionRetryPolicy = new TestRetryPolicy(0),
            };
            await using MqttSessionClient sessionClient = new(mockClient, options);
            try
            {
                TaskCompletionSource<MqttClientDisconnectedEventArgs> sessionLostArgsTcs = new();
                sessionClient.SessionLostAsync += (args) =>
                {
                    sessionLostArgsTcs.TrySetResult(args);
                    return Task.CompletedTask;
                };

                TaskCompletionSource requestEnqueued = new();
                mockClient.OnSubscribeAttempt += async (args) =>
                {
                    requestEnqueued.TrySetResult();
                    await Task.Delay(TimeSpan.FromHours(1));
                    throw new Exception("This should never be reached");
                };

                await sessionClient.ConnectAsync(GetClientOptions());

                // This operation won't finish prior to the disconnection and should eventually
                // report that it was cancelled upon retry policy being exhausted
                var subscribeTask = sessionClient.SubscribeAsync(new MqttClientSubscribeOptions("some/topic"));

                await mockClient.SimulateServerInitiatedDisconnectAsync(new MqttCommunicationException("some disconnect"));

                try
                {
                    await sessionLostArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("Timed out waiting for the session client to notify the application that the session was lost");
                }

                // Since the session was lost prior to this request being completed, the session client should notify the user
                // that the request failed because the session was lost.
                await Assert.ThrowsAsync<RetryExpiredException>(async () => await subscribeTask.WaitAsync(TimeSpan.FromSeconds(30)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_QueuedRequestsAreCancelledIfRetryIsExhausted_UnsubscribeAsync()
        {
            using MockMqttClient mockClient = new MockMqttClient();
            MqttSessionClientOptions options = new()
            {
                ConnectionRetryPolicy = new TestRetryPolicy(0),
            };
            await using MqttSessionClient sessionClient = new(mockClient, options);
            try
            {
                TaskCompletionSource<MqttClientDisconnectedEventArgs> sessionLostArgsTcs = new();
                sessionClient.SessionLostAsync += (args) =>
                {
                    sessionLostArgsTcs.TrySetResult(args);
                    return Task.CompletedTask;
                };

                TaskCompletionSource requestEnqueued = new();
                mockClient.OnUnsubscribeAttempt += async (args) =>
                {
                    requestEnqueued.TrySetResult();
                    await Task.Delay(TimeSpan.FromHours(1));
                    throw new Exception("This should never be reached");
                };

                await sessionClient.ConnectAsync(GetClientOptions());

                // This operation won't finish prior to the disconnection and should eventually
                // report that it was cancelled upon retry policy being exhausted
                var unsubscribeTask = sessionClient.UnsubscribeAsync(new MqttClientUnsubscribeOptions("some/topic"));

                await mockClient.SimulateServerInitiatedDisconnectAsync(new MqttCommunicationException("some disconnect"));

                try
                {
                    await sessionLostArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("Timed out waiting for the session client to notify the application that the session was lost");
                }

                // Since the session was lost prior to this request being completed, the session client should notify the user
                // that the request failed because the session was lost.
                await Assert.ThrowsAsync<RetryExpiredException>(async () => await unsubscribeTask.WaitAsync(TimeSpan.FromSeconds(30)));
            }
            finally
            {
                await sessionClient.DisconnectAsync();
            }
        }

        [Fact]
        public async Task MqttSessionClient_ThrowsIfAccessedWhenDisposed()
        {
            using MockMqttClient mockMqttClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockMqttClient);

            await sessionClient.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await sessionClient.ConnectAsync(new MqttClientOptions(new MqttClientTcpOptions("localhost", 1883))));
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await sessionClient.DisconnectAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await sessionClient.PublishAsync(new MqttApplicationMessage("sometopic")));
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await sessionClient.SubscribeAsync(new MqttClientSubscribeOptions("someTopic")));
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await sessionClient.UnsubscribeAsync(new MqttClientUnsubscribeOptions("someTopic")));
        }

        [Fact]
        public async Task MqttSessionClient_ThrowsIfCancellationRequested()
        {
            using MockMqttClient mockMqttClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockMqttClient);

            CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await sessionClient.ConnectAsync(new MqttClientOptions(new MqttClientTcpOptions("localhost", 1883)), cts.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await sessionClient.DisconnectAsync(cancellationToken: cts.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await sessionClient.PublishAsync(new MqttApplicationMessage("sometopic"), cancellationToken: cts.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await sessionClient.SubscribeAsync(new MqttClientSubscribeOptions("someTopic"), cancellationToken: cts.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await sessionClient.UnsubscribeAsync(new MqttClientUnsubscribeOptions("someTopic"), cancellationToken: cts.Token));
        }

        [Fact]
        public async Task MqttSessionClient_HandlesMultipleSimultaneousInvocationsToDisconnectedAsync()
        {
            using MockMqttClient mockMqttClient = new MockMqttClient();
            await using MqttSessionClient sessionClient = new(mockMqttClient);

            await sessionClient.ConnectAsync(GetClientOptions());

            int reconnectAttemptNumber = 0;
            mockMqttClient.OnConnectAttempt += (actualConnect) =>
            {
                reconnectAttemptNumber++;
                return Task.FromResult(new MQTTnet.MqttClientConnectResultFactory().Create(MockMqttClient.SuccessfulReconnectConnAck, MQTTnet.Formatter.MqttProtocolVersion.V500));
            };

            Exception someDisconnectException = new();
            _ = mockMqttClient.SimulateServerInitiatedDisconnectAsync(someDisconnectException, MQTTnet.MqttClientDisconnectReason.UnspecifiedError);
            _ = mockMqttClient.SimulateServerInitiatedDisconnectAsync(someDisconnectException, MQTTnet.MqttClientDisconnectReason.UnspecifiedError);

            // Once this returns, the reconnection should have finished
            await sessionClient.PublishAsync(new MqttApplicationMessage("some/topic")).WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(1, reconnectAttemptNumber);
        }


        private class TestCertificateProvider : IMqttClientCertificatesProvider
        {
            public X509Certificate2 CurrentCertificate { get; set; }

            public TestCertificateProvider(X509Certificate2 certificate)
            {
                CurrentCertificate = certificate;
            }

            public X509CertificateCollection GetCertificates()
            {
                return new X509CertificateCollection(new X509Certificate2[] { CurrentCertificate });
            }
        }

        // For test purposes only
        private static X509Certificate2 GenerateSelfSignedCertificate(string subjectName = "")
        {
            if (string.IsNullOrEmpty(subjectName))
            {
                subjectName = "Self-Signed-Cert-Example";
            }

            var ecdsa = ECDsa.Create(ECCurve.CreateFromValue("1.2.840.10045.3.1.7"));
            var certRequest = new CertificateRequest($"CN={subjectName}", ecdsa, HashAlgorithmName.SHA256);

            X509Certificate2 generatedCert = certRequest.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(10));
            X509Certificate2 pfxGeneratedCert = new X509Certificate2(generatedCert.Export(X509ContentType.Pfx));

            return pfxGeneratedCert;
        }

        private static MqttClientOptions GetClientOptions(MqttConnectionSettings? mcs = null)
        {
            if (mcs == null)
            {
                mcs = new MqttConnectionSettings("someHostname");
            }

            return new MqttClientOptions(mcs)
            {
                CleanSession = true,
                SessionExpiryInterval = 10,
            };
        }
    }
}