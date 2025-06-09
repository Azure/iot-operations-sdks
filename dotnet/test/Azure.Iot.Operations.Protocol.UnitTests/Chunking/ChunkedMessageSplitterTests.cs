// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Text.Json;
using Azure.Iot.Operations.Protocol.Chunking;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.UnitTests.Chunking;

public class ChunkedMessageSplitterTests
{
    [Fact]
    public void SplitMessage_SmallMessage_ThrowArgumentException()
    {
        // Arrange
        var options = new ChunkingOptions { Enabled = true, StaticOverhead = 100 };
        var splitter = new ChunkedMessageSplitter(options);

        var payload = "Small message that doesn't need chunking"u8.ToArray();
        var originalMessage = new MqttApplicationMessage("test/topic")
        {
            Payload = new ReadOnlySequence<byte>(payload),
            UserProperties = [new MqttUserProperty("originalProperty", "value")]
        };

        var maxPacketSize = 1000; // Large enough for the small message

        // Act & Assert
        // This should throw an exception because the message is too small to be chunked
        Assert.Throws<ArgumentException>(() => splitter.SplitMessage(originalMessage, maxPacketSize));
    }

    [Fact]
    public void SplitMessage_LargeMessage_ReturnsMultipleChunks()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            Enabled = true,
            StaticOverhead = 100,
            ChecksumAlgorithm = ChunkingChecksumAlgorithm.SHA256
        };
        var splitter = new ChunkedMessageSplitter(options);

        // Create a large payload (2500 bytes)
        var payloadSize = 2500;
        var payload = new byte[payloadSize];
        Random.Shared.NextBytes(payload);


        var originalMessage = new MqttApplicationMessage("test/topic")
        {
            Payload = new ReadOnlySequence<byte>(payload),
            UserProperties = [new MqttUserProperty("originalProperty", "value")]
        };

        // Set a max packet size that will force chunking
        // MaxChunkSize = MaxPacketSize - StaticOverhead = 900
        var maxPacketSize = 1000;

        // Act
        var chunks = splitter.SplitMessage(originalMessage, maxPacketSize);

        // Assert
        // Should have 3 chunks (2500 / 900 = 2.78 => 3 chunks)
        Assert.Equal(3, chunks.Count);

        // Verify each chunk has the chunk metadata property
        foreach (var chunk in chunks)
        {
            var chunkProperty = chunk.UserProperties?.FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty);
            Assert.NotNull(chunkProperty);

            // Check that original properties are preserved
            var originalProperty = chunk.UserProperties?.FirstOrDefault(p => p.Name == "originalProperty");
            Assert.NotNull(originalProperty);
            Assert.Equal("value", originalProperty!.Value);
        }

        // Verify the chunks contain all the original data
        var totalSize = chunks.Sum(c => c.Payload.Length);
        Assert.Equal(payloadSize, totalSize);

        // Reassemble and verify content
        var reassembledPayload = new byte[payloadSize];
        var offset = 0;

        foreach (var chunk in chunks)
        foreach (var segment in chunk.Payload)
        {
            segment.Span.CopyTo(reassembledPayload.AsSpan(offset));
            offset += segment.Length;
        }

        Assert.Equal(payload, reassembledPayload);
    }

    [Fact]
    public void SplitMessage_VerifyChunkMetadata_IsCorrect()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            Enabled = true,
            StaticOverhead = 100,
            ChecksumAlgorithm = ChunkingChecksumAlgorithm.SHA256,
        };
        var splitter = new ChunkedMessageSplitter(options);

        // Create a payload that needs to be split into exactly 2 chunks
        var chunkSize = 900; // maxPacketSize - staticOverhead
        var payloadSize = chunkSize + 100; // Just over one chunk
        var payload = new byte[payloadSize];
        Random.Shared.NextBytes(payload);

        var originalMessage = new MqttApplicationMessage("test/topic")
        {
            Payload = new ReadOnlySequence<byte>(payload),
            MessageExpiryInterval = 30u, // Set expiry interval to 30 seconds
        };

        var maxPacketSize = 1000;

        // Act
        var chunks = splitter.SplitMessage(originalMessage, maxPacketSize);

        // Assert
        Assert.Equal(2, chunks.Count);

        // Check first chunk metadata
        var firstChunkProperty = chunks[0].UserProperties?.FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty);
        Assert.NotNull(firstChunkProperty);
        var firstChunkMetadata = JsonSerializer.Deserialize<ChunkMetadata>(firstChunkProperty!.Value);

        // First chunk should contain totalChunks and checksum
        Assert.NotNull(firstChunkMetadata!.MessageId);
        Assert.NotNull(firstChunkMetadata.TotalChunks);
        Assert.NotNull(firstChunkMetadata.Checksum);

        Assert.Equal(0, firstChunkMetadata.ChunkIndex);
        Assert.Equal(2, firstChunkMetadata.TotalChunks);

        // Check that MessageExpiryInterval is set (30 seconds)
        Assert.Equal(30u, chunks[0].MessageExpiryInterval);

        // Get the messageId from the first chunk
        var messageId = firstChunkMetadata.MessageId;

        // Check second chunk metadata
        var secondChunkProperty = chunks[1].UserProperties?.FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty);
        Assert.NotNull(secondChunkProperty);
        var secondChunkMetadata = JsonSerializer.Deserialize<ChunkMetadata>(secondChunkProperty!.Value);

        // Second chunk should not contain totalChunks or checksum
        Assert.NotNull(secondChunkMetadata);
        Assert.NotNull(secondChunkMetadata!.MessageId);
        Assert.Null(secondChunkMetadata.TotalChunks);
        Assert.Null(secondChunkMetadata.Checksum);

        Assert.Equal(messageId, secondChunkMetadata.MessageId);
        Assert.Equal(1, secondChunkMetadata.ChunkIndex);

        // Check that MessageExpiryInterval is set on second chunk too (30 seconds)
        Assert.Equal(30u, chunks[1].MessageExpiryInterval);
    }

    [Fact]
    public void SplitMessage_ChecksumVerification_ValidChecksum()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            Enabled = true,
            StaticOverhead = 10,
            ChecksumAlgorithm = ChunkingChecksumAlgorithm.SHA256
        };
        var splitter = new ChunkedMessageSplitter(options);

        var payload = new byte[128];
        Random.Shared.NextBytes(payload);
        var originalMessage = new MqttApplicationMessage("test/topic")
        {
            Payload = new ReadOnlySequence<byte>(payload)
        };

        // Force chunking by using a small max packet size
        var maxPacketSize = 128;

        // Act
        var chunks = splitter.SplitMessage(originalMessage, maxPacketSize);

        // Get the checksum from the first chunk
        var firstChunkProperty = chunks[0].UserProperties?.FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty);
        var firstChunkMetadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(firstChunkProperty!.Value);
        var checksum = firstChunkMetadata![ChunkingConstants.ChecksumField].GetString();

        // Calculate the checksum directly using the same algorithm
        var calculatedChecksum = ChecksumCalculator.CalculateChecksum(
            new ReadOnlySequence<byte>(payload),
            ChunkingChecksumAlgorithm.SHA256);

        // Assert
        Assert.Equal(calculatedChecksum, checksum);
    }

    [Fact]
    public void SplitMessage_PreservesMessageProperties()
    {
        // Arrange
        var options = new ChunkingOptions { Enabled = true, StaticOverhead = 100 };
        var splitter = new ChunkedMessageSplitter(options);

        // Create a large payload that needs chunking
        var payloadSize = 1500;
        var payload = new byte[payloadSize];

        // Create a message with various properties
        var originalMessage = new MqttApplicationMessage("test/topic", MqttQualityOfServiceLevel.ExactlyOnce)
        {
            Payload = new ReadOnlySequence<byte>(payload),
            ContentType = "application/json",
            Retain = true,
            ResponseTopic = "response/topic",
            CorrelationData = [1, 2, 3, 4, 5, 6, 7, 8, 9],
            PayloadFormatIndicator = MqttPayloadFormatIndicator.Unspecified,
            MessageExpiryInterval = 3600,
            TopicAlias = 5,
            SubscriptionIdentifiers = [1, 2, 3],
            UserProperties =
            [
                new MqttUserProperty("prop1", "value1"),
                new MqttUserProperty("prop2", "value2")
            ]
        };

        var maxPacketSize = 1000;

        // Act
        var chunks = splitter.SplitMessage(originalMessage, maxPacketSize);

        // Assert - check that all chunks preserve the original message properties
        foreach (var chunk in chunks)
        {
            // Check basic properties
            Assert.Equal(originalMessage.Topic, chunk.Topic);
            Assert.Equal(originalMessage.QualityOfServiceLevel, chunk.QualityOfServiceLevel);
            Assert.Equal(originalMessage.ContentType, chunk.ContentType);
            Assert.Equal(originalMessage.Retain, chunk.Retain);
            Assert.Equal(originalMessage.ResponseTopic, chunk.ResponseTopic);
            Assert.Equal(originalMessage.PayloadFormatIndicator, chunk.PayloadFormatIndicator);
            Assert.Equal(originalMessage.MessageExpiryInterval, chunk.MessageExpiryInterval);
            Assert.Equal(originalMessage.TopicAlias, chunk.TopicAlias);

            // Check correlation data
            Assert.Equal(originalMessage.CorrelationData, chunk.CorrelationData);

            // Check subscription identifiers
            Assert.Equal(originalMessage.SubscriptionIdentifiers, chunk.SubscriptionIdentifiers);

            // Check user properties (excluding the chunk property)
            foreach (var originalProp in originalMessage.UserProperties!)
                Assert.Contains(chunk.UserProperties!, p =>
                    p.Name == originalProp.Name && p.Value == originalProp.Value);
        }
    }

    [Fact]
    public void SplitMessage_NullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new ChunkingOptions { Enabled = true };
        var splitter = new ChunkedMessageSplitter(options);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => splitter.SplitMessage(null!, 1000));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ChunkedMessageSplitter(null!));
    }

    [Fact]
    public void SplitMessage_MaxPacketSizeSmallerThanStaticOverhead_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            Enabled = true,
            StaticOverhead = 1000 // Larger than max packet size
        };
        var splitter = new ChunkedMessageSplitter(options);

        var payload = "Test message"u8.ToArray();
        var originalMessage = new MqttApplicationMessage("test/topic")
        {
            Payload = new ReadOnlySequence<byte>(payload)
        };

        var maxPacketSize = 500; // Smaller than the static overhead

        // Act & Assert
        // This should not throw
        Assert.Throws<ArgumentOutOfRangeException>(() => splitter.SplitMessage(originalMessage, maxPacketSize));
    }

    [Fact]
    public void Integration_SplitAndReassemble_RecoversOriginalMessage()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            Enabled = true,
            StaticOverhead = 1024, // 1 KB
            ChecksumAlgorithm = ChunkingChecksumAlgorithm.SHA256
        };

        var splitter = new ChunkedMessageSplitter(options);

        // Create a test payload
        var payloadSize = 1024 * 1024; // 1 MB
        var payload = new byte[payloadSize];
        Random.Shared.NextBytes(payload);

        var originalMessage = new MqttApplicationMessage("test/topic")
        {
            Payload = new ReadOnlySequence<byte>(payload),
            UserProperties = [new MqttUserProperty("originalProperty", "value")]
        };

        // Force chunking by using a small max packet size
        var maxPacketSize = 2048; // 2 KB

        // Act - Split the message
        var chunks = splitter.SplitMessage(originalMessage, maxPacketSize);

        // Now reassemble
        var assembler = new ChunkedMessageAssembler(0, options.ChecksumAlgorithm);

        // Get metadata from first chunk
        var firstChunkProperty = chunks[0].UserProperties!.First(p => p.Name == ChunkingConstants.ChunkUserProperty);
        var firstChunkMetadata = JsonSerializer.Deserialize<ChunkMetadata>(firstChunkProperty.Value);

        var totalChunks = firstChunkMetadata!.TotalChunks!.Value;
        var checksum = firstChunkMetadata.Checksum;

        // Update assembler with metadata
        assembler.UpdateMetadata(totalChunks, checksum, null);

        // Add all chunks
        foreach (var chunk in chunks)
        {
            // Extract chunk index from metadata
            var chunkProperty = chunk.UserProperties!.First(p => p.Name == ChunkingConstants.ChunkUserProperty);
            var chunkMetadata = JsonSerializer.Deserialize<ChunkMetadata>(chunkProperty.Value);

            // Simulate receiving the chunk
            assembler.AddChunk(chunkMetadata!.ChunkIndex, CreateMqttMessageEventArgs(chunk));
        }

        // Try to reassemble
        var success = assembler.TryReassemble(out var reassembledArgs);

        // Assert
        Assert.True(success);
        Assert.NotNull(reassembledArgs);

        // Verify the content is identical
        Assert.Equal(payload, reassembledArgs!.ApplicationMessage.Payload.ToArray());

        // Check that original properties are preserved but chunk metadata is removed
        var properties = reassembledArgs.ApplicationMessage.UserProperties;
        Assert.Contains(properties!, p => p.Name == "originalProperty" && p.Value == "value");
        Assert.DoesNotContain(properties!, p => p.Name == ChunkingConstants.ChunkUserProperty);
    }

    // Helper method to create message event args for testing
    private static MqttApplicationMessageReceivedEventArgs CreateMqttMessageEventArgs(MqttApplicationMessage message)
    {
        return new MqttApplicationMessageReceivedEventArgs(
            "testClient",
            message,
            1,
            (_, _) => Task.CompletedTask);
    }
}
