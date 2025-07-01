// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Chunking;
using Azure.Iot.Operations.Protocol.Models;
using System.Buffers;

namespace Azure.Iot.Operations.Protocol.IntegrationTests
{
    public class ChunkingMqttClientTests
    {
        [Fact]
        public async Task ChunkingMqttClient_SmallMessage_NoChunking()
        {
            // Arrange
            // Create a base client
            await using var mqttClient = await ClientFactory.CreateExtendedClientAsyncFromEnvAsync(Guid.NewGuid().ToString());

            // Create a chunking client with modest settings
            var options = new ChunkingOptions
            {
                Enabled = true,
                StaticOverhead = 500 // Use modest overhead to ensure small messages aren't chunked
            };

            await using var chunkingClient = new ChunkingMqttPubSubClient(mqttClient, options);

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
        }

        [Fact]
        public async Task ChunkingMqttClient_LargeMessage_ChunkingAndReassembly()
        {
            // Arrange
            // Create a base client
            await using var mqttClient = await ClientFactory.CreateExtendedClientAsyncFromEnvAsync(Guid.NewGuid().ToString());

            // Create a chunking client with settings that force chunking
            var options = new ChunkingOptions
            {
                Enabled = true,
                StaticOverhead = 500
            };

            await using var chunkingClient = new ChunkingMqttPubSubClient(mqttClient, options);

            var messageReceivedTcs = new TaskCompletionSource<MqttApplicationMessage>();
            chunkingClient.ApplicationMessageReceivedAsync += (args) =>
            {
                messageReceivedTcs.TrySetResult(args.ApplicationMessage);
                return Task.CompletedTask;
            };

            // Subscribe to a unique topic
            var topic = $"chunking/test/{Guid.NewGuid()}";
            await chunkingClient.SubscribeAsync(new MqttClientSubscribeOptions(topic, MqttQualityOfServiceLevel.AtLeastOnce));

            // TODO: @maximsemenov80 for the test purpose UpdateMaxPacketSizeFromConnectResult artificially set MaxPacketSize to 64KB
            var largePayloadSize = 1024 * 1024; // 1MB
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
        }
    }
}
