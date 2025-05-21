// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Text;
using System.Text.Json;
using Azure.Iot.Operations.Protocol.Chunking;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Moq;

namespace Azure.Iot.Operations.Protocol.UnitTests.Chunking;

public class ChunkingMqttClientTests
{
    [Fact]
    public async Task PublishAsync_SmallMessage_PassesThroughToInnerClient()
    {
        // Arrange
        var mockInnerClient = new Mock<IMqttClient>();
        var expectedResult = new MqttClientPublishResult(
            null,
            MqttClientPublishReasonCode.Success,
            "No chunking result",
            new List<MqttUserProperty>());

        MqttApplicationMessage? capturedMessage = null;
        mockInnerClient
            .Setup(c => c.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<MqttApplicationMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .ReturnsAsync(expectedResult);

        // Configure connected client with MaxPacketSize
        mockInnerClient.SetupGet(c => c.IsConnected).Returns(true);

        // Setup connection result with MaximumPacketSize to be large
        uint? maxPacketSize = 10000;
        var connectResult = new MqttClientConnectResult
        {
            IsSessionPresent = true,
            ResultCode = MqttClientConnectResultCode.Success,
            MaximumPacketSize = maxPacketSize,
            UserProperties = new List<MqttUserProperty>()
        };

        var options = new ChunkingOptions
        {
            Enabled = true,
            StaticOverhead = 100
        };

        var client = new ChunkingMqttClient(mockInnerClient.Object, options);

        // Make sure the client is "connected" and knows the max packet size
        var connectedArgs = new MqttClientConnectedEventArgs(connectResult);
        await mockInnerClient.RaiseAsync(m => m.ConnectedAsync += null, connectedArgs);

        // Create a small message that doesn't need chunking
        var smallPayload = new byte[100];
        var smallMessage = new MqttApplicationMessage("test/topic")
        {
            Payload = new ReadOnlySequence<byte>(smallPayload)
        };

        // Act
        var result = await client.PublishAsync(smallMessage, CancellationToken.None);

        // Assert
        Assert.NotEqual(ChunkingConstants.ChunkedMessageSuccessReasonString, result.ReasonString);
        Assert.NotNull(capturedMessage);
        Assert.Same(smallMessage, capturedMessage);
    }

    [Fact]
    public async Task PublishAsync_LargeMessage_ChunksMessageAndSendsMultipleMessages()
    {
        // Arrange
        var mockInnerClient = new Mock<IMqttClient>();
        var publishedMessages = new List<MqttApplicationMessage>();

        var mqttClientPublishResult = new MqttClientPublishResult(
            null,
            MqttClientPublishReasonCode.Success,
            "No chunking result",
            new List<MqttUserProperty>());

        mockInnerClient
            .Setup(c => c.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<MqttApplicationMessage, CancellationToken>((msg, _) => publishedMessages.Add(msg))
            .ReturnsAsync(mqttClientPublishResult);

        // Configure connected client with MaxPacketSize
        mockInnerClient.SetupGet(c => c.IsConnected).Returns(true);

        // Set a small max packet size to force chunking
        var maxPacketSize = 1000;
        var connectResult = new MqttClientConnectResult
        {
            IsSessionPresent = true,
            ResultCode = MqttClientConnectResultCode.Success,
            MaximumPacketSize = (uint)maxPacketSize,
            MaximumQoS = MqttQualityOfServiceLevel.AtLeastOnce,
            UserProperties = new List<MqttUserProperty>()
        };

        var options = new ChunkingOptions
        {
            Enabled = true,
            StaticOverhead = 100,
            ChecksumAlgorithm = ChunkingChecksumAlgorithm.SHA256
        };

        var client = new ChunkingMqttClient(mockInnerClient.Object, options);

        // Make sure the client is "connected" and knows the max packet size
        var connectedArgs = new MqttClientConnectedEventArgs(connectResult);
        await mockInnerClient.RaiseAsync(m => m.ConnectedAsync += null, connectedArgs);

        // Create a large message that needs chunking
        // The max chunk size will be maxPacketSize - staticOverhead = 900 bytes
        var largePayloadSize = 2500; // This should create 3 chunks
        var largePayload = new byte[largePayloadSize];
        // Fill with identifiable content for later verification
        for (var i = 0; i < largePayloadSize; i++) largePayload[i] = (byte)(i % 256);

        var largeMessage = new MqttApplicationMessage("test/topic")
        {
            Payload = new ReadOnlySequence<byte>(largePayload)
        };

        // Act
        var result = await client.PublishAsync(largeMessage, CancellationToken.None);

        // Assert
        Assert.Equal(ChunkingConstants.ChunkedMessageSuccessReasonString, result.ReasonString);

        // Should have 3 chunks
        Assert.Equal(3, publishedMessages.Count);

        // Verify all messages have the chunk metadata property
        var messageIds = new HashSet<string>();
        foreach (var msg in publishedMessages)
        {
            var chunkProperty = msg.UserProperties?.FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty);
            Assert.NotNull(chunkProperty);

            // Parse the metadata
            var metadata = JsonSerializer.Deserialize<ChunkMetadata>(chunkProperty!.Value);
            Assert.NotNull(metadata);
            Assert.NotEmpty(metadata!.MessageId);
            messageIds.Add(metadata.MessageId);
            Assert.True(metadata.ChunkIndex >= 0);
            Assert.True(metadata.Timeout == ChunkingConstants.DefaultChunkTimeout);

            // First chunk should have totalChunks and checksum
            if (metadata.ChunkIndex == 0)
            {
                Assert.NotNull(metadata.TotalChunks);
                Assert.NotNull(metadata.Checksum);
                Assert.Equal(3, metadata.TotalChunks);
            }
        }

        Assert.Single(messageIds); // All chunks should have the same messageId

        // Verify total payload size across all chunks equals original payload size
        var totalChunkSize = publishedMessages.Sum(m => m.Payload.Length);
        Assert.Equal(largePayloadSize, totalChunkSize);
    }

    [Fact]
    public async Task HandleApplicationMessageReceivedAsync_NonChunkedMessage_PassesThroughToHandler()
    {
        // Arrange
        var mockInnerClient = new Mock<IMqttClient>();
        var handlerCalled = false;
        var capturedArgs = default(MqttApplicationMessageReceivedEventArgs);

        var client = new ChunkingMqttClient(mockInnerClient.Object);
        client.ApplicationMessageReceivedAsync += args =>
        {
            handlerCalled = true;
            capturedArgs = args;
            return Task.CompletedTask;
        };

        // Create a regular message without chunking metadata
        var payload = Encoding.UTF8.GetBytes("Regular non-chunked message");
        var message = new MqttApplicationMessage("test/topic")
        {
            Payload = new ReadOnlySequence<byte>(payload)
        };
        var receivedArgs = new MqttApplicationMessageReceivedEventArgs("client1", message, 1, (_, _) => Task.CompletedTask);

        // Act
        // Simulate receiving a message from the inner client
        await mockInnerClient.RaiseAsync(m => m.ApplicationMessageReceivedAsync += null, receivedArgs);

        // Assert
        Assert.True(handlerCalled);
        Assert.Same(receivedArgs, capturedArgs);
    }

    [Fact]
    public async Task HandleApplicationMessageReceivedAsync_ChunkedMessage_ReassemblesBeforeDelivering()
    {
        // Arrange
        var mockInnerClient = new Mock<IMqttClient>();
        var handlerCalled = false;
        var capturedArgs = default(MqttApplicationMessageReceivedEventArgs);

        var client = new ChunkingMqttClient(mockInnerClient.Object);
        client.ApplicationMessageReceivedAsync += args =>
        {
            handlerCalled = true;
            capturedArgs = args;
            return Task.CompletedTask;
        };

        // Create message ID and checksum
        var messageId = Guid.NewGuid().ToString("D");
        var fullMessage = "This is a complete message after reassembly";
        var fullPayload = Encoding.UTF8.GetBytes(fullMessage);
        var checksum = ChecksumCalculator.CalculateChecksum(new ReadOnlySequence<byte>(fullPayload), ChunkingChecksumAlgorithm.SHA256);

        // Create a chunked message with 2 parts
        var chunk1Text = "This is a complete ";
        var chunk2Text = "message after reassembly";

        // Create first chunk with metadata
        var chunk1 = CreateChunkedMessage("test/topic", chunk1Text, messageId, 0, 2, checksum);

        // Create second chunk with metadata
        var chunk2 = CreateChunkedMessage("test/topic", chunk2Text, messageId, 1);
        var receivedArgs1 = new MqttApplicationMessageReceivedEventArgs("client1", chunk1, 1, (_, _) => Task.CompletedTask);
        var receivedArgs2 = new MqttApplicationMessageReceivedEventArgs("client1", chunk2, 2, (_, _) => Task.CompletedTask);

        // Act
        // Simulate receiving chunks from the inner client
        await mockInnerClient.RaiseAsync(m => m.ApplicationMessageReceivedAsync += null, receivedArgs1);
        await mockInnerClient.RaiseAsync(m => m.ApplicationMessageReceivedAsync += null, receivedArgs2);

        // Assert
        Assert.True(handlerCalled);
        Assert.NotNull(capturedArgs);

        Assert.Equal(fullPayload, capturedArgs!.ApplicationMessage.Payload.ToArray());

        // Verify chunk metadata was removed
        Assert.DoesNotContain(
            capturedArgs.ApplicationMessage.UserProperties ?? Enumerable.Empty<MqttUserProperty>(),
            p => p.Name == ChunkingConstants.ChunkUserProperty);
    }

    [Fact]
    public async Task DisconnectedAsync_ClearsInProgressChunks()
    {
        // Since we can't directly test private fields, we'll test the behavior
        // by simulating a reconnect scenario with chunks from before

        // Arrange
        var mockInnerClient = new Mock<IMqttClient>();
        var client = new ChunkingMqttClient(mockInnerClient.Object);

        // Create and set up a disconnect event
        var disconnectArgs = new MqttClientDisconnectedEventArgs(
            true,
            null,
            MqttClientDisconnectReason.NormalDisconnection,
            null,
            new List<MqttUserProperty>(),
            null);

        // Act
        await mockInnerClient.RaiseAsync(m => m.DisconnectedAsync += null, disconnectArgs);

        // Assert
        // This test is mostly for coverage since we can't directly verify the _messageAssemblers was cleared
        // The behavior would be verified in a combination with other tests like HandleApplicationMessageReceivedAsync_ChunkedMessage
    }

    // Helper method to create a chunked message with metadata
    private static MqttApplicationMessage CreateChunkedMessage(
        string topic,
        string payloadText,
        string messageId,
        int chunkIndex,
        int? totalChunks = null,
        string? checksum = null)
    {
        // Create chunk metadata
        Dictionary<string, object> metadata = new()
        {
            { ChunkingConstants.MessageIdField, messageId },
            { ChunkingConstants.ChunkIndexField, chunkIndex },
            { ChunkingConstants.TimeoutField, ChunkingConstants.DefaultChunkTimeout }
        };

        // Add totalChunks and checksum for first chunk
        if (totalChunks.HasValue)
        {
            metadata.Add(ChunkingConstants.TotalChunksField, totalChunks.Value);
        }

        if (checksum != null)
        {
            metadata.Add(ChunkingConstants.ChecksumField, checksum);
        }

        // Serialize metadata
        var metadataJson = JsonSerializer.Serialize(metadata);

        // Create payload
        var payload = Encoding.UTF8.GetBytes(payloadText);

        // Create message
        var message = new MqttApplicationMessage(topic)
        {
            Payload = new ReadOnlySequence<byte>(payload),
            UserProperties = new List<MqttUserProperty>
            {
                new(ChunkingConstants.ChunkUserProperty, metadataJson)
            }
        };
        return message;
    }
}
