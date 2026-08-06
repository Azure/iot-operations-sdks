// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Protocol.Chunking;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.UnitTests.Chunking;

public class ChunkedMessageSplitterTests
{
    private const int MaxPacketSize = 1200;
    private const int SafetyMargin = 16;

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ChunkedMessageSplitter(null!));
    }

    [Fact]
    public void SplitMessage_NullMessage_ThrowsArgumentNullException()
    {
        var splitter = new ChunkedMessageSplitter(new ChunkingOptions());

        Assert.Throws<ArgumentNullException>(() => splitter.SplitMessage(null!, MaxPacketSize));
    }

    [Fact]
    public void SplitMessage_MaxPacketSizeBelowProtocolMinimum_ThrowsArgumentOutOfRangeException()
    {
        var splitter = new ChunkedMessageSplitter(new ChunkingOptions());
        var message = NewMessage(NewPayload(4096));

        Assert.Throws<ArgumentOutOfRangeException>(() => splitter.SplitMessage(message, 64));
    }

    [Fact]
    public void SplitMessage_ProducesAHeaderChunkThenDataChunks()
    {
        var chunks = Split(NewPayload(4096));

        Assert.True(chunks.Count > 2);

        // The header chunk carries the metadata and none of the payload.
        Assert.Equal(0, chunks[0].Payload.Length);
        Assert.True(ChunkMetadata.TryParse(ChunkValue(chunks[0]), out var head));
        Assert.Equal(0, head!.ChunkIndex);
        Assert.Equal(chunks.Count, head.TotalChunks);
        Assert.NotNull(head.Checksum);

        // Data chunks are indexed from 1 and carry neither total nor checksum.
        for (int i = 1; i < chunks.Count; i++)
        {
            Assert.True(chunks[i].Payload.Length > 0);
            Assert.True(ChunkMetadata.TryParse(ChunkValue(chunks[i]), out var data));
            Assert.Equal(i, data!.ChunkIndex);
            Assert.Null(data.TotalChunks);
            Assert.Null(data.Checksum);
            Assert.Equal(head.MessageId, data.MessageId);
        }
    }

    [Fact]
    public void SplitMessage_DataChunksCoverThePayloadExactlyAndInOrder()
    {
        var payload = NewPayload(4096);
        var chunks = Split(payload);

        var reassembled = chunks.SelectMany(c => c.Payload.ToArray()).ToArray();

        Assert.Equal(payload.Length, chunks.Sum(c => c.Payload.Length));
        Assert.Equal(payload, reassembled);
    }

    [Fact]
    public void SplitMessage_EveryChunkFitsWithinTheMaximumPacketSize()
    {
        // Enough properties to dominate the header chunk, but still short of filling it.
        var properties = Enumerable.Range(0, 12)
            .Select(i => new MqttUserProperty($"userProperty{i:D2}", new string('v', 40)))
            .ToList();

        var chunks = Split(NewPayload(4096), properties);

        Assert.All(chunks, c => Assert.True(
            MqttPacketSizeEstimator.EstimatePublishSize(c) <= MaxPacketSize,
            $"A chunk estimated at {MqttPacketSizeEstimator.EstimatePublishSize(c)} bytes exceeds {MaxPacketSize}."));
    }

    [Fact]
    public void SplitMessage_OnlyTheHeaderChunkCarriesTheOriginalProperties()
    {
        List<MqttUserProperty> properties =
        [
            new("originalProperty", "value"),
            new("$partition", "client-1"),
            new("__protVer", "1.0"),
        ];

        var chunks = Split(NewPayload(4096), properties);

        Assert.Contains(chunks[0].UserProperties!, p => p.Name == "originalProperty");

        foreach (var chunk in chunks.Skip(1))
        {
            Assert.DoesNotContain(chunk.UserProperties!, p => p.Name == "originalProperty");

            // Routing and per-chunk validation properties must survive on every chunk.
            Assert.Contains(chunk.UserProperties!, p => p.Name == "$partition");
            Assert.Contains(chunk.UserProperties!, p => p.Name == "__protVer");
        }
    }

    [Fact]
    public void SplitMessage_PropertiesTooLargeForOnePacket_Throws()
    {
        // The properties alone cannot be split, so the message is undeliverable and must say so.
        var properties = Enumerable.Range(0, 40)
            .Select(i => new MqttUserProperty($"userProperty{i:D2}", new string('v', 200)))
            .ToList();

        var splitter = new ChunkedMessageSplitter(new ChunkingOptions { StaticOverhead = SafetyMargin });
        var message = NewMessage(NewPayload(4096), properties);

        var exception = Assert.Throws<ArgumentException>(() => splitter.SplitMessage(message, MaxPacketSize));
        Assert.Contains("exceeds the maximum packet size", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SplitMessage_PreservesMessageProperties()
    {
        var message = new MqttApplicationMessage("test/topic", MqttQualityOfServiceLevel.ExactlyOnce)
        {
            Payload = new ReadOnlySequence<byte>(NewPayload(4096)),
            ContentType = "application/json",
            Retain = true,
            ResponseTopic = "response/topic",
            CorrelationData = [1, 2, 3, 4, 5, 6, 7, 8, 9],
            PayloadFormatIndicator = MqttPayloadFormatIndicator.Unspecified,
            MessageExpiryInterval = 3600,
            TopicAlias = 5,
        };

        var chunks = new ChunkedMessageSplitter(new ChunkingOptions { StaticOverhead = SafetyMargin })
            .SplitMessage(message, MaxPacketSize);

        foreach (var chunk in chunks)
        {
            Assert.Equal(message.Topic, chunk.Topic);
            Assert.Equal(message.QualityOfServiceLevel, chunk.QualityOfServiceLevel);
            Assert.Equal(message.ContentType, chunk.ContentType);
            Assert.Equal(message.Retain, chunk.Retain);
            Assert.Equal(message.ResponseTopic, chunk.ResponseTopic);
            Assert.Equal(message.PayloadFormatIndicator, chunk.PayloadFormatIndicator);
            Assert.Equal(message.MessageExpiryInterval, chunk.MessageExpiryInterval);
            Assert.Equal(message.TopicAlias, chunk.TopicAlias);
            Assert.Equal(message.CorrelationData, chunk.CorrelationData);
        }
    }

    [Fact]
    public void SplitMessage_ChecksumCoversTheWholePayload()
    {
        var payload = NewPayload(4096);
        var chunks = Split(payload);

        Assert.True(ChunkMetadata.TryParse(ChunkValue(chunks[0]), out var head));

        var expected = ChecksumCalculator.CalculateChecksum(
            new ReadOnlySequence<byte>(payload),
            ChunkingChecksumAlgorithm.SHA256);

        Assert.Equal(expected, head!.Checksum);
    }

    [Fact]
    public void SplitIfNeeded_SmallMessage_IsReturnedUnchanged()
    {
        var message = NewMessage("small"u8.ToArray());

        var result = ChunkedMessageSplitter.SplitIfNeeded(message);

        Assert.Same(message, Assert.Single(result));
    }

    [Fact]
    public void SplitIfNeeded_LargePayload_IsChunked()
    {
        var message = NewMessage(NewPayload(ChunkingConstants.PlaceholderMaxPacketSize * 2));

        var result = ChunkedMessageSplitter.SplitIfNeeded(message);

        Assert.True(result.Count > 1);
    }

    [Fact]
    public void SplitIfNeeded_PayloadUnderTheLimitButPacketOverIt_IsChunked()
    {
        // The trigger is the encoded packet size, not the payload length. This payload would have
        // passed a payload-only check, but the properties push the whole packet over the limit.
        var properties = Enumerable.Range(0, 60)
            .Select(i => new MqttUserProperty($"userProperty{i:D3}", new string('v', 400)))
            .ToList();

        var payload = NewPayload(48 * 1024);
        var message = NewMessage(payload, properties);

        Assert.True(payload.Length < ChunkingConstants.PlaceholderMaxPacketSize);
        Assert.True(MqttPacketSizeEstimator.EstimatePublishSize(message) > ChunkingConstants.PlaceholderMaxPacketSize);

        var result = ChunkedMessageSplitter.SplitIfNeeded(message);

        Assert.True(result.Count > 1);
        Assert.Equal(0, result[0].Payload.Length);
        Assert.Contains(result[0].UserProperties!, p => p.Name == "userProperty000");
    }

    [Fact]
    public void Integration_SplitAndReassemble_RecoversOriginalMessage()
    {
        var payload = NewPayload(1024 * 1024);
        var message = NewMessage(payload, [new MqttUserProperty("originalProperty", "value")]);

        var chunks = ChunkedMessageSplitter.SplitIfNeeded(message);
        Assert.True(chunks.Count > 1);

        var buffer = new ChunkBuffer(new ChunkingOptions());
        DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        ChunkBufferResult? result = null;
        foreach (var chunk in chunks)
        {
            result = buffer.AddChunk(Received(chunk), now, now.AddMinutes(1));
        }

        Assert.NotNull(result!.ReassembledMessage);
        Assert.Equal(payload, result.ReassembledMessage!.ApplicationMessage.Payload.ToArray());

        var properties = result.ReassembledMessage.ApplicationMessage.UserProperties;
        Assert.Contains(properties!, p => p.Name == "originalProperty" && p.Value == "value");
        Assert.DoesNotContain(properties!, p => p.Name == ChunkingConstants.ChunkUserProperty);
    }

    private static string? ChunkValue(MqttApplicationMessage message) =>
        message.UserProperties?.FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty)?.Value;

    private static byte[] NewPayload(int size)
    {
        var payload = new byte[size];
        Random.Shared.NextBytes(payload);
        return payload;
    }

    private static MqttApplicationMessage NewMessage(byte[] payload, List<MqttUserProperty>? properties = null) =>
        new("test/topic")
        {
            Payload = new ReadOnlySequence<byte>(payload),
            ResponseTopic = "test/response",
            CorrelationData = Guid.NewGuid().ToByteArray(),
            MessageExpiryInterval = 30u,
            UserProperties = properties,
        };

    private static IReadOnlyList<MqttApplicationMessage> Split(byte[] payload, List<MqttUserProperty>? properties = null) =>
        new ChunkedMessageSplitter(new ChunkingOptions { StaticOverhead = SafetyMargin })
            .SplitMessage(NewMessage(payload, properties), MaxPacketSize);

    private static MqttApplicationMessageReceivedEventArgs Received(MqttApplicationMessage message) =>
        new("testClient", message, 1, (_, _) => Task.CompletedTask);
}
