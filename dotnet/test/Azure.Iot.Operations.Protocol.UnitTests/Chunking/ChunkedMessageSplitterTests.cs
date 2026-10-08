// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Protocol.Chunking;
using Azure.Iot.Operations.Protocol.Chunking.Exceptions;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.UnitTests.Chunking;

public class ChunkedMessageSplitterTests
{
    private const int MaxPacketSize = 1200;

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
        Assert.Equal(ChunkKind.Head, head.Kind);
        Assert.NotNull(head.Checksum);

        // Data chunks are indexed from 1 and repeat the total, but not the checksum.
        for (int i = 1; i < chunks.Count; i++)
        {
            Assert.True(chunks[i].Payload.Length > 0);
            Assert.True(ChunkMetadata.TryParse(ChunkValue(chunks[i]), out var data));
            Assert.Equal(i, data!.ChunkIndex);
            Assert.Equal(chunks.Count, data.TotalChunks);
            Assert.Equal(ChunkKind.Data, data.Kind);
            Assert.Null(data.Checksum);
            Assert.Equal(head.MessageId, data.MessageId);
        }
    }

    [Fact]
    public void SplitMessage_PropertyChunksPreserveOriginalOrder()
    {
        List<MqttUserProperty> properties = Enumerable.Range(0, 30)
            .Select(i => new MqttUserProperty($"property{i:D2}", new string((char)('a' + (i % 26)), 80)))
            .ToList();

        var chunks = Split(NewPayload(4096), properties);
        var propertyChunks = chunks.Where(c => Metadata(c).Kind == ChunkKind.Property).ToList();

        Assert.True(propertyChunks.Count > 1);
        Assert.All(propertyChunks, c => Assert.Equal(0, c.Payload.Length));
        Assert.All(propertyChunks, c => Assert.NotEqual(
            c.UserProperties!.Count - 1,
            c.UserProperties.FindIndex(p => p.Name == ChunkingConstants.ChunkUserProperty)));
        Assert.All(chunks.Where(c => Metadata(c).Kind != ChunkKind.Property), c => Assert.Equal(
            c.UserProperties!.Count - 1,
            c.UserProperties.FindIndex(p => p.Name == ChunkingConstants.ChunkUserProperty)));

        List<MqttUserProperty> reassembled = propertyChunks
            .SelectMany(PropertiesAfterChunkMetadata)
            .ToList();

        Assert.Equal(properties.Select(p => (p.Name, p.Value)), reassembled.Select(p => (p.Name, p.Value)));
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
            MqttPacketSizeCalculator.CalculatePublishSize(c) <= MaxPacketSize,
            $"A chunk of {MqttPacketSizeCalculator.CalculatePublishSize(c)} bytes exceeds {MaxPacketSize}."));
    }

    [Fact]
    public void SplitMessage_OnlyPropertyChunksCarryOriginalProperties()
    {
        List<MqttUserProperty> properties =
        [
            new("originalProperty", "value"),
            new("$partition", "client-1"),
            new("__protVer", "1.0"),
        ];

        var chunks = Split(NewPayload(4096), properties);

        Assert.DoesNotContain(chunks[0].UserProperties!, p => p.Name == "originalProperty");

        foreach (var chunk in chunks.Skip(1))
        {
            // Routing and per-chunk validation properties must survive on every chunk.
            Assert.Contains(chunk.UserProperties!, p => p.Name == "$partition");
            Assert.Contains(chunk.UserProperties!, p => p.Name == "__protVer");
        }

        MqttApplicationMessage propertyChunk = Assert.Single(chunks.Where(c => Metadata(c).Kind == ChunkKind.Property));
        Assert.Contains(PropertiesAfterChunkMetadata(propertyChunk), p => p.Name == "originalProperty");
        Assert.All(chunks.Where(c => Metadata(c).Kind != ChunkKind.Property),
            c => Assert.Empty(PropertiesAfterChunkMetadata(c)));
    }

    [Fact]
    public void SplitMessage_LargePropertySet_IsSplitAcrossChunks()
    {
        var properties = Enumerable.Range(0, 40)
            .Select(i => new MqttUserProperty($"userProperty{i:D2}", new string('v', 200)))
            .ToList();

        var chunks = Split(NewPayload(4096), properties);

        Assert.True(chunks.Count(c => Metadata(c).Kind == ChunkKind.Property) > 1);
        Assert.All(chunks, c => Assert.True(MqttPacketSizeCalculator.CalculatePublishSize(c) <= MaxPacketSize));
    }

    [Fact]
    public void SplitMessage_SinglePropertyTooLargeForOneChunk_Throws()
    {
        List<MqttUserProperty> properties = [new("oversized", new string('v', MaxPacketSize * 2))];
        var splitter = new ChunkedMessageSplitter(new ChunkingOptions());

        var exception = Assert.Throws<MessageTooLargeError>(() => splitter.SplitMessage(NewMessage(NewPayload(16), properties), MaxPacketSize));

        Assert.Contains("cannot fit in one property chunk", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SplitMessage_CallerSuppliedChunkProperty_Throws()
    {
        var message = NewMessage(NewPayload(16), [new MqttUserProperty(ChunkingConstants.ChunkUserProperty, "caller")]);
        var splitter = new ChunkedMessageSplitter(new ChunkingOptions());

        ChunkAssemblyError exception = Assert.Throws<ChunkAssemblyError>(() => splitter.SplitMessage(message, MaxPacketSize));

        Assert.Contains("reserved", exception.Message, StringComparison.OrdinalIgnoreCase);
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

        var chunks = new ChunkedMessageSplitter(new ChunkingOptions())
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

        var expected = ChunkChecksums.Sha256.Compute(new ReadOnlySequence<byte>(payload));

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
        Assert.True(MqttPacketSizeCalculator.CalculatePublishSize(message) > ChunkingConstants.PlaceholderMaxPacketSize);

        var result = ChunkedMessageSplitter.SplitIfNeeded(message);

        Assert.True(result.Count > 1);
        Assert.Equal(0, result[0].Payload.Length);
        Assert.Contains(result.Where(c => Metadata(c).Kind == ChunkKind.Property)
            .SelectMany(PropertiesAfterChunkMetadata), p => p.Name == "userProperty000");
    }

    [Fact]
    public void SplitMessage_WithoutDeliveryAllowance_AccountsForRemainingLengthGrowth()
    {
        const int packetLimit = 65_536;
        var splitter = new ChunkedMessageSplitter(new ChunkingOptions());

        IReadOnlyList<MqttApplicationMessage> chunks = splitter.SplitMessage(
            NewMessage(NewPayload(packetLimit * 2)),
            packetLimit);

        Assert.All(chunks, chunk => Assert.True(
            MqttPacketSizeCalculator.CalculatePublishSize(chunk) <= packetLimit,
            $"Encoded chunk size {MqttPacketSizeCalculator.CalculatePublishSize(chunk)} exceeds {packetLimit}."));
    }

    [Fact]
    public void SplitIfNeeded_RequestCountdownReservationFitsAfterStamping()
    {
        MqttApplicationMessage message = NewMessage(NewPayload(ChunkingConstants.PlaceholderMaxPacketSize * 2));

        IReadOnlyList<MqttApplicationMessage> chunks = ChunkedMessageSplitter.SplitIfNeeded(
            message,
            includeRemainingSeconds: true);

        Assert.All(chunks, chunk =>
        {
            Assert.True(MqttPacketSizeCalculator.CalculatePublishSize(chunk) <= ChunkingConstants.PlaceholderMaxPacketSize);
            Assert.True(ChunkMetadata.TryParse(ChunkValue(chunk), out ChunkMetadata? metadata));
            Assert.Equal(uint.MaxValue, metadata!.RemainingSeconds);
        });
    }

    [Fact]
    public void SplitMessage_NoPayloadByteCanFit_ThrowsMessageTooLarge()
    {
        const string messageId = "8ac7a0e4-1b3d-4f9a-9a3f-0d2f6c5b7e11";
        string? contentType = null;
        int packetLimit = 0;
        for (int length = 1; length < 20_000; length++)
        {
            string candidate = new('x', length);
            var probe = new MqttApplicationMessage("t")
            {
                ContentType = candidate,
                UserProperties =
                [
                    new(ChunkingConstants.ChunkUserProperty, ChunkMetadata.CreateDataChunk(messageId, 99, 100).Format()),
                ],
            };
            long emptySize = MqttPacketSizeCalculator.CalculatePublishSize(probe, 0);
            if (MqttPacketSizeCalculator.CalculatePublishSize(probe, 1) > emptySize + 1)
            {
                contentType = candidate;
                packetLimit = (int)emptySize + 1;
                break;
            }
        }

        Assert.NotNull(contentType);
        var message = new MqttApplicationMessage("t")
        {
            ContentType = contentType,
            Payload = new ReadOnlySequence<byte>([1]),
        };

        MessageTooLargeError exception = Assert.Throws<MessageTooLargeError>(
            () => new ChunkedMessageSplitter(new ChunkingOptions()).SplitMessage(message, packetLimit));

        Assert.Contains("cannot carry any payload", exception.Message, StringComparison.Ordinal);
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

    private static ChunkMetadata Metadata(MqttApplicationMessage message)
    {
        Assert.True(ChunkMetadata.TryParse(ChunkValue(message), out ChunkMetadata? metadata));
        return metadata!;
    }

    private static IEnumerable<MqttUserProperty> PropertiesAfterChunkMetadata(MqttApplicationMessage message)
    {
        List<MqttUserProperty> properties = message.UserProperties ?? [];
        int metadataIndex = properties.FindIndex(p => p.Name == ChunkingConstants.ChunkUserProperty);
        return metadataIndex < 0 ? [] : properties.Skip(metadataIndex + 1);
    }

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
        new ChunkedMessageSplitter(new ChunkingOptions())
            .SplitMessage(NewMessage(payload, properties), MaxPacketSize);

    private static MqttApplicationMessageReceivedEventArgs Received(MqttApplicationMessage message) =>
        new("testClient", message, 1, (_, _) => Task.CompletedTask);
}
