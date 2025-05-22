// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Chunking;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Iot.Operations.Protocol.IntegrationTests.Chunking
{
    public class ChunkingMqttClientIntegrationTests
    {
        [Fact]
        public async Task ChunkingMqttClient_SmallMessage_NoChunking()
        {
            // Arrange
            // Create a base client
            var baseClient = await ClientFactory.CreateClientAsyncFromEnvAsync(Guid.NewGuid().ToString());

            // Create a chunking client with modest settings
            var options = new ChunkingOptions
            {
                Enabled = true,
                StaticOverhead = 500, // Use modest overhead to ensure small messages aren't chunked
                ChunkTimeout = TimeSpan.FromSeconds(10)
            };

            await using var chunkingClient = new ChunkingMqttClient(baseClient, options);

            var messageReceivedTcs = new TaskCompletionSource<MqttApplicationMessage>();
            chunkingClient.ApplicationMessageReceivedAsync += (args) =>
            {
                messageReceivedTcs.TrySetResult(args.ApplicationMessage);
                return Task.CompletedTask;
            };

            // Subscribe to a unique topic
            var topic = $"chunking/test/{Guid.NewGuid()}";
            await chunkingClient.SubscribeAsync(new MqttClientSubscribeOptions(topic, MqttQualityOfServiceLevel.AtLeastOnce));

            // Create a small message - 100 bytes payload
            var smallPayload = new byte[100];
            Random.Shared.NextBytes(smallPayload);

            var message = new MqttApplicationMessage(topic, MqttQualityOfServiceLevel.AtLeastOnce)
            {
                Payload = new ReadOnlySequence<byte>(smallPayload),
                UserProperties = new List<MqttUserProperty>
                {
                    new("testProperty", "testValue")
                }
            };

            // Act
            var publishResult = await chunkingClient.PublishAsync(message);

            // Wait for the message to be received - timeout after 10 seconds
            MqttApplicationMessage? receivedMessage = null;
            try
            {
                receivedMessage = await messageReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for the message to be received");
            }

            // Assert
            Assert.NotNull(receivedMessage);

            // Verify payload is identical
            Assert.Equal(smallPayload, receivedMessage.Payload.ToArray());

            // Verify no chunking metadata was added
            var chunkProperty = receivedMessage.UserProperties?.FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty);
            Assert.Null(chunkProperty);

            // Verify original properties were preserved
            var testProperty = receivedMessage.UserProperties?.FirstOrDefault(p => p.Name == "testProperty");
            Assert.NotNull(testProperty);
            Assert.Equal("testValue", testProperty!.Value);

            await chunkingClient.DisconnectAsync();
        }

        [Fact]
        public async Task ChunkingMqttClient_LargeMessage_ChunkingAndReassembly()
        {
            // Arrange
            // Create a base client
            var baseClient = await ClientFactory.CreateClientAsyncFromEnvAsync(Guid.NewGuid().ToString());

            // Create a chunking client with settings that force chunking
            var options = new ChunkingOptions
            {
                Enabled = true,
                StaticOverhead = 500,
                ChunkTimeout = TimeSpan.FromSeconds(30)
            };

            await using var chunkingClient = new ChunkingMqttClient(baseClient, options);

            var messageReceivedTcs = new TaskCompletionSource<MqttApplicationMessage>();
            chunkingClient.ApplicationMessageReceivedAsync += (args) =>
            {
                messageReceivedTcs.TrySetResult(args.ApplicationMessage);
                return Task.CompletedTask;
            };

            // Subscribe to a unique topic
            var topic = $"chunking/test/{Guid.NewGuid()}";
            await chunkingClient.SubscribeAsync(new MqttClientSubscribeOptions(topic, MqttQualityOfServiceLevel.AtLeastOnce));

            // Create a large message - 100KB payload to force chunking
            // Most MQTT brokers have default max packet size <= 64KB
            var largePayloadSize = 1024 * 1024; // 1MB to ensure chunking
            var largePayload = new byte[largePayloadSize];

            // Fill with recognizable pattern for verification
            for (int i = 0; i < largePayloadSize; i++)
            {
                largePayload[i] = (byte)(i % 256);
            }

            var message = new MqttApplicationMessage(topic, MqttQualityOfServiceLevel.AtLeastOnce)
            {
                Payload = new ReadOnlySequence<byte>(largePayload),
                UserProperties = new List<MqttUserProperty>
                {
                    new("testProperty", "testValue")
                }
            };

            // Act
            var publishResult = await chunkingClient.PublishAsync(message);

            // Wait for the reassembled message to be received - timeout after 30 seconds
            // Reassembly may take longer than a normal message
            MqttApplicationMessage? receivedMessage = null;
            try
            {
                receivedMessage = await messageReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for the reassembled message to be received");
            }

            // Assert
            Assert.NotNull(receivedMessage);

            // Verify payload size is correct
            Assert.Equal(largePayloadSize, receivedMessage.Payload.Length);

            // Verify payload content is identical
            var reassembledPayload = receivedMessage.Payload.ToArray();
            Assert.Equal(largePayload, reassembledPayload);

            // Verify chunking metadata was removed
            var chunkProperty = receivedMessage.UserProperties?.FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty);
            Assert.Null(chunkProperty);

            // Verify original properties were preserved
            var testProperty = receivedMessage.UserProperties?.FirstOrDefault(p => p.Name == "testProperty");
            Assert.NotNull(testProperty);
            Assert.Equal("testValue", testProperty!.Value);

            await chunkingClient.DisconnectAsync();
        }

        /*
        [Fact]
        public async Task ChunkingMqttClient_MessageWithComplexProperties_PreservesAllProperties()
        {
            // Arrange
            // Create a base client
            var baseClient = await ClientFactory.CreateClientAsyncFromEnvAsync(Guid.NewGuid().ToString());

            // Create a chunking client with settings that force chunking
            var options = new ChunkingOptions
            {
                Enabled = true,
                StaticOverhead = 500,
                ChunkTimeout = TimeSpan.FromSeconds(30)
            };

            await using var chunkingClient = new ChunkingMqttClient(baseClient, options);

            var messageReceivedTcs = new TaskCompletionSource<MqttApplicationMessage>();
            chunkingClient.ApplicationMessageReceivedAsync += (args) =>
            {
                messageReceivedTcs.TrySetResult(args.ApplicationMessage);
                return Task.CompletedTask;
            };

            // Subscribe to a unique topic
            var topic = $"chunking/test/{Guid.NewGuid()}";
            await chunkingClient.SubscribeAsync(new MqttClientSubscribeOptions(topic, MqttQualityOfServiceLevel.AtLeastOnce));

            // Create a large message with various MQTT properties
            var payloadSize = 50 * 1024; // 50KB to ensure chunking
            var payload = new byte[payloadSize];
            Random.Shared.NextBytes(payload);

            var correlationData = Encoding.UTF8.GetBytes("correlation-data-value");

            var message = new MqttApplicationMessage(topic, MqttQualityOfServiceLevel.ExactlyOnce)
            {
                Payload = new ReadOnlySequence<byte>(payload),
                ContentType = "application/json",
                ResponseTopic = "response/topic/path",
                CorrelationData = new ReadOnlySequence<byte>(correlationData),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.Utf8,
                MessageExpiryInterval = 3600,
                Retain = true,
                UserProperties = new List<MqttUserProperty>
                {
                    new("prop1", "value1"),
                    new("prop2", "value2"),
                    new("prop3", "value3")
                }
            };

            // Act
            var publishResult = await chunkingClient.PublishAsync(message);

            // Wait for the reassembled message to be received
            MqttApplicationMessage? receivedMessage = null;
            try
            {
                receivedMessage = await messageReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for the reassembled message to be received");
            }

            // Assert
            Assert.NotNull(receivedMessage);

            // Verify all properties were preserved
            Assert.Equal(message.Topic, receivedMessage.Topic);
            Assert.Equal(message.QualityOfServiceLevel, receivedMessage.QualityOfServiceLevel);
            Assert.Equal(message.ContentType, receivedMessage.ContentType);
            Assert.Equal(message.ResponseTopic, receivedMessage.ResponseTopic);
            Assert.Equal(correlationData, receivedMessage.CorrelationData.ToArray());
            Assert.Equal(message.PayloadFormatIndicator, receivedMessage.PayloadFormatIndicator);
            Assert.Equal(message.MessageExpiryInterval, receivedMessage.MessageExpiryInterval);
            Assert.Equal(message.Retain, receivedMessage.Retain);

            // Verify user properties were preserved
            Assert.Contains(receivedMessage.UserProperties!, p => p.Name == "prop1" && p.Value == "value1");
            Assert.Contains(receivedMessage.UserProperties!, p => p.Name == "prop2" && p.Value == "value2");
            Assert.Contains(receivedMessage.UserProperties!, p => p.Name == "prop3" && p.Value == "value3");

            await chunkingClient.DisconnectAsync();
        }

        [Fact]
        public async Task ChunkingMqttClient_MultipleClients_CanExchangeChunkedMessages()
        {
            // Arrange
            // Create two base clients
            var baseClient1 = await ClientFactory.CreateClientAsyncFromEnvAsync(Guid.NewGuid().ToString());
            var baseClient2 = await ClientFactory.CreateClientAsyncFromEnvAsync(Guid.NewGuid().ToString());

            // Create chunking clients
            var options = new ChunkingOptions
            {
                Enabled = true,
                StaticOverhead = 500,
                ChunkTimeout = TimeSpan.FromSeconds(30)
            };

            await using var chunkingClient1 = new ChunkingMqttClient(baseClient1, options);
            await using var chunkingClient2 = new ChunkingMqttClient(baseClient2, options);

            var messageReceivedTcs = new TaskCompletionSource<MqttApplicationMessage>();
            chunkingClient2.ApplicationMessageReceivedAsync += (args) =>
            {
                messageReceivedTcs.TrySetResult(args.ApplicationMessage);
                return Task.CompletedTask;
            };

            // Subscribe client2 to a unique topic
            var topic = $"chunking/test/{Guid.NewGuid()}";
            await chunkingClient2.SubscribeAsync(new MqttClientSubscribeOptions(topic, MqttQualityOfServiceLevel.AtLeastOnce));

            // Wait briefly to ensure subscription is established
            await Task.Delay(1000);

            // Create a large message on client1
            var payloadSize = 80 * 1024; // 80KB
            var payload = new byte[payloadSize];
            Random.Shared.NextBytes(payload);

            var message = new MqttApplicationMessage(topic, MqttQualityOfServiceLevel.AtLeastOnce)
            {
                Payload = new ReadOnlySequence<byte>(payload)
            };

            // Act
            var publishResult = await chunkingClient1.PublishAsync(message);

            // Wait for client2 to receive the reassembled message
            MqttApplicationMessage? receivedMessage = null;
            try
            {
                receivedMessage = await messageReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for the message to be received");
            }

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal(payloadSize, receivedMessage.Payload.Length);
            Assert.Equal(payload, receivedMessage.Payload.ToArray());

            await chunkingClient1.DisconnectAsync();
            await chunkingClient2.DisconnectAsync();
        }

        [Fact]
        public async Task ChunkingMqttClient_Reconnection_ClearsInProgressReassembly()
        {
            // This test verifies that incomplete reassembly state is properly cleared on disconnect

            // Arrange
            var baseClient = await ClientFactory.CreateClientAsyncFromEnvAsync(Guid.NewGuid().ToString());

            var options = new ChunkingOptions
            {
                Enabled = true,
                StaticOverhead = 500,
                ChunkTimeout = TimeSpan.FromMinutes(5) // Long timeout to ensure it doesn't expire naturally
            };

            await using var chunkingClient = new ChunkingMqttClient(baseClient, options);

            // Counter for message reception
            int messagesReceived = 0;
            var firstMessageTcs = new TaskCompletionSource<MqttApplicationMessage>();
            var secondMessageTcs = new TaskCompletionSource<MqttApplicationMessage>();

            chunkingClient.ApplicationMessageReceivedAsync += (args) =>
            {
                messagesReceived++;
                if (messagesReceived == 1)
                {
                    firstMessageTcs.TrySetResult(args.ApplicationMessage);
                }
                else if (messagesReceived == 2)
                {
                    secondMessageTcs.TrySetResult(args.ApplicationMessage);
                }
                return Task.CompletedTask;
            };

            // Subscribe to a topic
            var topic = $"chunking/test/{Guid.NewGuid()}";
            await chunkingClient.SubscribeAsync(new MqttClientSubscribeOptions(topic, MqttQualityOfServiceLevel.AtLeastOnce));

            // Create two identical messages
            var payload = new byte[70 * 1024]; // Large enough to ensure chunking
            Random.Shared.NextBytes(payload);

            var message = new MqttApplicationMessage(topic, MqttQualityOfServiceLevel.AtLeastOnce)
            {
                Payload = new ReadOnlySequence<byte>(payload)
            };

            // Act - Part 1: Send first message
            await chunkingClient.PublishAsync(message);

            // Wait for first message to arrive
            var firstMessage = await firstMessageTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.NotNull(firstMessage);

            // Disconnect and reconnect
            await chunkingClient.DisconnectAsync();
            await Task.Delay(1000); // Brief pause
            await chunkingClient.ReconnectAsync();

            // Resubscribe
            await chunkingClient.SubscribeAsync(new MqttClientSubscribeOptions(topic, MqttQualityOfServiceLevel.AtLeastOnce));
            await Task.Delay(1000); // Brief pause to ensure subscription is established

            // Act - Part 2: Send second message
            await chunkingClient.PublishAsync(message);

            // Wait for second message
            var secondMessage = await secondMessageTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));

            // Assert
            Assert.NotNull(secondMessage);
            Assert.Equal(2, messagesReceived); // Both messages should be received and reassembled
            Assert.Equal(payload, secondMessage.Payload.ToArray());

            await chunkingClient.DisconnectAsync();
        }

        [Fact(Skip = "This test requires special broker configuration and manual verification")]
        public async Task ChunkingMqttClient_MessageExceedingBrokerMaxSize_HandlesProperly()
        {
            // This test requires a broker with a known maximum message size
            // The test would need to be adjusted based on the broker configuration

            // Arrange
            var baseClient = await ClientFactory.CreateClientAsyncFromEnvAsync(Guid.NewGuid().ToString());

            var options = new ChunkingOptions
            {
                Enabled = true,
                StaticOverhead = 500,
                ChunkTimeout = TimeSpan.FromSeconds(30)
            };

            await using var chunkingClient = new ChunkingMqttClient(baseClient, options);

            var messageReceivedTcs = new TaskCompletionSource<MqttApplicationMessage>();
            chunkingClient.ApplicationMessageReceivedAsync += (args) =>
            {
                messageReceivedTcs.TrySetResult(args.ApplicationMessage);
                return Task.CompletedTask;
            };

            // Subscribe to a topic
            var topic = $"chunking/test/{Guid.NewGuid()}";
            await chunkingClient.SubscribeAsync(new MqttClientSubscribeOptions(topic, MqttQualityOfServiceLevel.AtLeastOnce));

            // Create a very large message (adjust size based on broker limits)
            // Example for a broker with 256KB max packet size:
            var payloadSize = 500 * 1024; // 500KB to ensure exceeding broker limits
            var payload = new byte[payloadSize];
            Random.Shared.NextBytes(payload);

            var message = new MqttApplicationMessage(topic, MqttQualityOfServiceLevel.AtLeastOnce)
            {
                Payload = new ReadOnlySequence<byte>(payload)
            };

            // Act
            var publishResult = await chunkingClient.PublishAsync(message);

            // Wait for reassembled message
            var receivedMessage = await messageReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(60));

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal(payloadSize, receivedMessage.Payload.Length);
            Assert.Equal(payload, receivedMessage.Payload.ToArray());

            await chunkingClient.DisconnectAsync();
        }
    */
    }
}
