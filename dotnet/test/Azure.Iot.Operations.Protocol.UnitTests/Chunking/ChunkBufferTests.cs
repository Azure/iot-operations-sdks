// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Protocol.Chunking;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.UnitTests.Chunking;

public class ChunkBufferTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ExpiresAt = Now.AddSeconds(10);

    // Small enough to produce a handful of chunks from a modest payload. The splitter measures a
    // data chunk's real overhead, so the payload budget is whatever is left after that plus the
    // safety margin, rather than a number these tests can compute.
    private const int TestMaxPacketSize = 1200;

    [Fact]
    public void IsChunk_UnchunkedMessage_ReturnsFalse()
    {
        var message = new MqttApplicationMessage("test/topic")
        {
            UserProperties = [new MqttUserProperty("__protVer", "1.0")]
        };

        Assert.False(ChunkBuffer.IsChunk(message));
    }

    [Fact]
    public void IsChunk_ChunkedMessage_ReturnsTrue()
    {
        var chunks = Split(NewPayload(4096));

        Assert.True(ChunkBuffer.IsChunk(chunks[0]));
    }

    [Fact]
    public void Split_ProducesHeadPropertyAndDataChunks()
    {
        var chunks = Split(NewPayload(4096), extraProperty: true);

        Assert.True(chunks.Count > 2);
        Assert.Equal(0, chunks[0].Payload.Length);
        Assert.Equal(ChunkKind.Head, Metadata(chunks[0]).Kind);

        MqttApplicationMessage propertyChunk = Assert.Single(chunks.Where(c => Metadata(c).Kind == ChunkKind.Property));
        Assert.Equal(0, propertyChunk.Payload.Length);
        Assert.Contains(propertyChunk.UserProperties!, p => p.Name == "originalProperty");

        List<MqttApplicationMessage> dataChunks = chunks.Where(c => Metadata(c).Kind == ChunkKind.Data).ToList();
        Assert.NotEmpty(dataChunks);
        Assert.All(dataChunks, c => Assert.True(c.Payload.Length > 0));
    }

    [Fact]
    public void Split_EveryChunkFitsWithinTheMaximumPacketSize()
    {
        var chunks = Split(NewPayload(4096), extraProperty: true);

        Assert.All(chunks, c => Assert.True(
            MqttPacketSizeCalculator.CalculatePublishSize(c) <= TestMaxPacketSize,
            $"A chunk of {MqttPacketSizeCalculator.CalculatePublishSize(c)} bytes exceeds {TestMaxPacketSize}."));
    }

    [Fact]
    public void AddChunk_AllChunksInOrder_ReassemblesPayload()
    {
        var payload = NewPayload(4096);
        var chunks = Split(payload);
        var buffer = new ChunkBuffer(new ChunkingOptions());

        Assert.True(chunks.Count > 1);

        for (int i = 0; i < chunks.Count - 1; i++)
        {
            var partial = buffer.AddChunk(Received(chunks[i]), Now, ExpiresAt);

            Assert.Null(partial.ReassembledMessage);
            Assert.Empty(partial.DiscardedChunks);
        }

        var result = buffer.AddChunk(Received(chunks[^1]), Now, ExpiresAt);

        Assert.NotNull(result.ReassembledMessage);
        Assert.Empty(result.DiscardedChunks);
        Assert.Equal(payload, result.ReassembledMessage!.ApplicationMessage.Payload.ToArray());
    }

    [Fact]
    public void AddChunk_ChunksOutOfOrder_ReassemblesPayload()
    {
        var payload = NewPayload(4096);
        var chunks = Split(payload, extraProperty: true);
        var buffer = new ChunkBuffer(new ChunkingOptions());

        ChunkBufferResult? result = null;
        foreach (var chunk in chunks.Reverse())
        {
            result = buffer.AddChunk(Received(chunk), Now, ExpiresAt);
        }

        Assert.NotNull(result!.ReassembledMessage);
        Assert.Equal(payload, result.ReassembledMessage!.ApplicationMessage.Payload.ToArray());
        Assert.Contains(result.ReassembledMessage.ApplicationMessage.UserProperties!,
            p => p.Name == "originalProperty" && p.Value == "value");
    }

    [Fact]
    public void AddChunk_ReassembledMessage_StripsChunkPropertyAndKeepsOriginalProperties()
    {
        var chunks = Split(NewPayload(4096), extraProperty: true);
        var buffer = new ChunkBuffer(new ChunkingOptions());

        ChunkBufferResult? result = null;
        foreach (var chunk in chunks)
        {
            result = buffer.AddChunk(Received(chunk), Now, ExpiresAt);
        }

        var properties = result!.ReassembledMessage!.ApplicationMessage.UserProperties;
        Assert.DoesNotContain(properties!, p => p.Name == ChunkingConstants.ChunkUserProperty);
        Assert.Contains(properties!, p => p.Name == "originalProperty" && p.Value == "value");
    }

    [Fact]
    public async Task AddChunk_AcknowledgingReassembledMessage_AcknowledgesEveryChunk()
    {
        var chunks = Split(NewPayload(4096));
        var buffer = new ChunkBuffer(new ChunkingOptions());
        var acknowledged = new List<int>();

        ChunkBufferResult? result = null;
        for (int i = 0; i < chunks.Count; i++)
        {
            int index = i;
            result = buffer.AddChunk(Received(chunks[i], () => acknowledged.Add(index)), Now, ExpiresAt);
        }

        Assert.Empty(acknowledged);

        await result!.ReassembledMessage!.AcknowledgeAsync(CancellationToken.None);

        Assert.Equal(Enumerable.Range(0, chunks.Count), acknowledged.OrderBy(i => i));
    }

    [Fact]
    public async Task AddChunk_RedeliveredChunk_ReleasesOldDeliveryAndHoldsNewDelivery()
    {
        var chunks = Split(NewPayload(4096));
        var buffer = new ChunkBuffer(new ChunkingOptions());
        var acknowledged = new List<string>();

        Assert.True(chunks.Count > 2);

        buffer.AddChunk(Received(chunks[0], () => acknowledged.Add("0")), Now, ExpiresAt);
        buffer.AddChunk(Received(chunks[1], () => acknowledged.Add("1-first")), Now, ExpiresAt);

        var redelivery = buffer.AddChunk(Received(chunks[1], () => acknowledged.Add("1-redelivered")), Now, ExpiresAt);

        Assert.Null(redelivery.ReassembledMessage);
        MqttApplicationMessageReceivedEventArgs displaced = Assert.Single(redelivery.DiscardedChunks);
        await displaced.AcknowledgeAsync(CancellationToken.None);
        Assert.Equal(["1-first"], acknowledged);

        ChunkBufferResult? result = null;
        for (int i = 2; i < chunks.Count; i++)
        {
            int index = i;
            result = buffer.AddChunk(Received(chunks[i], () => acknowledged.Add(index.ToString())), Now, ExpiresAt);
        }

        await result!.ReassembledMessage!.AcknowledgeAsync(CancellationToken.None);

        Assert.Contains("1-redelivered", acknowledged);
    }

    [Fact]
    public async Task AddChunk_DuplicateIndexWithDifferentPacketIdentifier_ReleasesDisplacedDelivery()
    {
        var chunks = Split(NewPayload(4096));
        await using var buffer = new ChunkBuffer(new ChunkingOptions());
        var acknowledged = new List<string>();

        buffer.AddChunk(Received(chunks[0], packetIdentifier: 10), Now, ExpiresAt);
        buffer.AddChunk(Received(chunks[1], () => acknowledged.Add("first"), 11), Now, ExpiresAt);

        var duplicate = buffer.AddChunk(Received(chunks[1], () => acknowledged.Add("replacement"), 12), Now, ExpiresAt);
        MqttApplicationMessageReceivedEventArgs displaced = Assert.Single(duplicate.DiscardedChunks);
        await displaced.AcknowledgeAsync(CancellationToken.None);

        Assert.Equal(["first"], acknowledged);

        ChunkBufferResult? completed = null;
        for (int i = 2; i < chunks.Count; i++)
        {
            completed = buffer.AddChunk(Received(chunks[i], packetIdentifier: (ushort)(12 + i)), Now, ExpiresAt);
        }

        await completed!.ReassembledMessage!.AcknowledgeAsync(CancellationToken.None);

        Assert.Contains("replacement", acknowledged);
    }

    [Fact]
    public async Task AddChunk_SameDeliveryInstanceTwice_IsAcknowledgedOnceOnCompletion()
    {
        IReadOnlyList<MqttApplicationMessage> chunks = Split(NewPayload(4096));
        await using var buffer = new ChunkBuffer(new ChunkingOptions());
        int headAcknowledgements = 0;
        var head = Received(chunks[0], () => headAcknowledgements++);

        buffer.AddChunk(head, Now, ExpiresAt);
        ChunkBufferResult repeated = buffer.AddChunk(head, Now, ExpiresAt);
        Assert.Empty(repeated.DiscardedChunks);

        ChunkBufferResult? completed = null;
        foreach (MqttApplicationMessage chunk in chunks.Skip(1))
        {
            completed = buffer.AddChunk(Received(chunk), Now, ExpiresAt);
        }

        await completed!.ReassembledMessage!.AcknowledgeAsync(CancellationToken.None);
        Assert.Equal(1, headAcknowledgements);
    }

    [Fact]
    public async Task DisposeAsync_WithPartialEntries_AcknowledgesEachHeldChunkOnce()
    {
        IReadOnlyList<MqttApplicationMessage> chunks = Split(NewPayload(4096));
        var buffer = new ChunkBuffer(new ChunkingOptions());
        int acknowledgements = 0;
        buffer.AddChunk(Received(chunks[0], () => acknowledgements++), Now, ExpiresAt);
        buffer.AddChunk(Received(chunks[1], () => acknowledgements++), Now, ExpiresAt);

        await buffer.DisposeAsync();
        await buffer.DisposeAsync();

        Assert.Equal(2, acknowledgements);
    }

    [Fact]
    public async Task AddChunk_AfterDispose_ReturnsDeliveryForAcknowledgement()
    {
        IReadOnlyList<MqttApplicationMessage> chunks = Split(NewPayload(4096));
        var buffer = new ChunkBuffer(new ChunkingOptions());
        await buffer.DisposeAsync();
        var delivery = Received(chunks[0]);

        ChunkBufferResult result = buffer.AddChunk(delivery, Now, ExpiresAt);

        Assert.Same(delivery, Assert.Single(result.DiscardedChunks));
    }

    [Fact]
    public void AddChunk_UnparsableMetadata_DiscardsAndReturnsChunkForAcknowledgement()
    {
        var buffer = new ChunkBuffer(new ChunkingOptions());
        var message = new MqttApplicationMessage("test/topic")
        {
            Payload = new ReadOnlySequence<byte>(NewPayload(16)),
            MessageExpiryInterval = 10u,
            UserProperties = [new MqttUserProperty(ChunkingConstants.ChunkUserProperty, "not-valid-metadata")]
        };
        var args = Received(message);

        var result = buffer.AddChunk(args, Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Same(args, Assert.Single(result.DiscardedChunks));
    }

    [Fact]
    public void AddChunk_DuplicateChunk_ReturnsDisplacedDelivery()
    {
        var chunks = Split(NewPayload(4096));
        var buffer = new ChunkBuffer(new ChunkingOptions());

        var first = Received(chunks[0]);
        Assert.Empty(buffer.AddChunk(first, Now, ExpiresAt).DiscardedChunks);

        var redelivery = Received(chunks[0]);
        var result = buffer.AddChunk(redelivery, Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Same(first, Assert.Single(result.DiscardedChunks));
    }

    [Fact]
    public void AddChunk_NonHeadChunkDeclaringTooManyChunks_DiscardsMessageImmediately()
    {
        var chunks = Split(NewPayload(4096));
        var buffer = new ChunkBuffer(new ChunkingOptions { MaxChunkCount = 2 });

        Assert.True(chunks.Count > 2);

        var result = buffer.AddChunk(Received(chunks[^1]), Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Single(result.DiscardedChunks);
    }

    [Fact]
    public void AddChunk_ConflictingTotals_DiscardsEveryHeldChunk()
    {
        var chunks = Split(NewPayload(4096));
        var buffer = new ChunkBuffer(new ChunkingOptions());
        var held = Received(chunks[^1]);

        Assert.Empty(buffer.AddChunk(held, Now, ExpiresAt).DiscardedChunks);

        ChunkMetadata head = Metadata(chunks[0]);
        ReplaceChunkMetadata(chunks[0], ChunkMetadata.CreateFirstChunk(
            head.MessageId,
            head.TotalChunks + 1,
            head.ChecksumId!,
            head.Checksum!).Format());
        var conflicting = Received(chunks[0]);
        var result = buffer.AddChunk(conflicting, Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Equal([held, conflicting], result.DiscardedChunks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddChunk_ConflictingHeadChecksumMetadata_DiscardsEveryHeldChunk(bool changeChecksumId)
    {
        var chunks = Split(NewPayload(4096));
        var buffer = new ChunkBuffer(new ChunkingOptions
        {
            ResolveChecksum = _ => ChunkChecksums.Sha256,
        });
        var held = Received(chunks[0]);
        ChunkMetadata head = Metadata(chunks[0]);

        Assert.Empty(buffer.AddChunk(held, Now, ExpiresAt).DiscardedChunks);

        ReplaceChunkMetadata(chunks[0], ChunkMetadata.CreateFirstChunk(
            head.MessageId,
            head.TotalChunks,
            changeChecksumId ? "other" : head.ChecksumId!,
            changeChecksumId ? head.Checksum! : "deadbeef").Format());
        var conflicting = Received(chunks[0]);
        var result = buffer.AddChunk(conflicting, Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Equal([held, conflicting], result.DiscardedChunks);
    }

    [Fact]
    public void AddChunk_PropertyIndexAfterHeldDataIndex_DiscardsEveryHeldChunk()
    {
        var chunks = Split(NewPayload(4096), extraProperty: true);
        MqttApplicationMessage property = chunks.Single(c => Metadata(c).Kind == ChunkKind.Property);
        MqttApplicationMessage data = chunks.First(c => Metadata(c).Kind == ChunkKind.Data);
        ChunkMetadata propertyMetadata = Metadata(property);
        ChunkMetadata dataMetadata = Metadata(data);
        ReplaceChunkMetadata(property, ChunkMetadata.CreatePropertyChunk(
            propertyMetadata.MessageId,
            dataMetadata.ChunkIndex + 1,
            propertyMetadata.TotalChunks).Format());
        var buffer = new ChunkBuffer(new ChunkingOptions());
        var held = Received(data);

        Assert.Empty(buffer.AddChunk(held, Now, ExpiresAt).DiscardedChunks);

        var conflicting = Received(property);
        var result = buffer.AddChunk(conflicting, Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Equal([held, conflicting], result.DiscardedChunks);
    }

    [Fact]
    public async Task AddChunk_PartialMessageExpiresWithoutMoreTraffic_AcknowledgesHeldChunks()
    {
        var chunks = Split(NewPayload(4096));
        var acknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var buffer = new ChunkBuffer(new ChunkingOptions());

        var held = Received(chunks[0], () => acknowledged.TrySetResult());
        Assert.Empty(buffer.AddChunk(held, Now, Now.AddMilliseconds(25)).DiscardedChunks);

        await acknowledged.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddChunk_HeadOnlyMessage_ReassemblesEmptyMessage()
    {
        const string messageId = "8ac7a0e4-1b3d-4f9a-9a3f-0d2f6c5b7e11";
        ChunkMetadata metadata = ChunkMetadata.CreateFirstChunk(
            messageId,
            1,
            ChunkChecksums.Sha256.Id,
            ChunkChecksums.Sha256.Compute(ReadOnlySequence<byte>.Empty));
        var message = new MqttApplicationMessage("test/topic")
        {
            MessageExpiryInterval = 10,
            UserProperties = [new MqttUserProperty(ChunkingConstants.ChunkUserProperty, metadata.Format())],
        };
        var buffer = new ChunkBuffer(new ChunkingOptions());

        ChunkBufferResult result = buffer.AddChunk(Received(message), Now, ExpiresAt);

        Assert.NotNull(result.ReassembledMessage);
        Assert.True(result.ReassembledMessage.ApplicationMessage.Payload.IsEmpty);
    }

    [Fact]
    public void AddChunk_EmptyPayloadWithProperties_ReassemblesWithoutDataChunks()
    {
        IReadOnlyList<MqttApplicationMessage> chunks = Split([], extraProperty: true);
        var buffer = new ChunkBuffer(new ChunkingOptions());

        Assert.Equal(2, chunks.Count);
        Assert.DoesNotContain(chunks, chunk => Metadata(chunk).Kind == ChunkKind.Data);

        ChunkBufferResult? result = null;
        foreach (MqttApplicationMessage chunk in chunks)
        {
            result = buffer.AddChunk(Received(chunk), Now, ExpiresAt);
        }

        Assert.NotNull(result!.ReassembledMessage);
        Assert.True(result.ReassembledMessage.ApplicationMessage.Payload.IsEmpty);
        Assert.Contains(
            result.ReassembledMessage.ApplicationMessage.UserProperties!,
            property => property.Name == "originalProperty" && property.Value == "value");
    }

    [Fact]
    public async Task AddChunk_CountdownBeyondSingleTimerRange_DoesNotPoisonEntry()
    {
        IReadOnlyList<MqttApplicationMessage> chunks = Split(NewPayload(4096));
        foreach (MqttApplicationMessage chunk in chunks)
        {
            ChunkMetadata metadata = Metadata(chunk);
            ReplaceChunkMetadata(chunk, metadata.Format(uint.MaxValue));
        }

        await using var buffer = new ChunkBuffer(new ChunkingOptions());
        DateTime now = DateTime.UtcNow;
        ChunkBufferResult? result = null;
        foreach (MqttApplicationMessage chunk in chunks)
        {
            result = buffer.AddChunk(
                Received(chunk),
                now,
                now.AddSeconds(1),
                requireRemainingSeconds: true);
        }

        Assert.NotNull(result!.ReassembledMessage);
    }

    [Fact]
    public async Task AddChunk_PeerCountdownBeyondCallerDeadline_IsClamped()
    {
        IReadOnlyList<MqttApplicationMessage> chunks = Split(NewPayload(4096));
        ChunkMetadata metadata = Metadata(chunks[0]);
        ReplaceChunkMetadata(chunks[0], metadata.Format(uint.MaxValue));
        var acknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var buffer = new ChunkBuffer(new ChunkingOptions());
        DateTime now = DateTime.UtcNow;

        buffer.AddChunk(
            Received(chunks[0], () => acknowledged.TrySetResult()),
            now,
            now.AddMilliseconds(25),
            requireRemainingSeconds: true);

        // The caller's nearer deadline wins over the peer's multi-decade countdown.
        await acknowledged.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddChunk_SameMessageIdWithDifferentCorrelations_ReassemblesIndependently()
    {
        byte[] firstPayload = NewPayload(4096);
        byte[] secondPayload = NewPayload(4096);
        IReadOnlyList<MqttApplicationMessage> first = Split(firstPayload);
        IReadOnlyList<MqttApplicationMessage> second = Split(secondPayload);
        string sharedMessageId = Metadata(first[0]).MessageId;
        foreach (MqttApplicationMessage chunk in second)
        {
            ChunkMetadata original = Metadata(chunk);
            ChunkMetadata replacement = original.Kind switch
            {
                ChunkKind.Head => ChunkMetadata.CreateFirstChunk(sharedMessageId, original.TotalChunks, original.ChecksumId!, original.Checksum!),
                ChunkKind.Property => ChunkMetadata.CreatePropertyChunk(sharedMessageId, original.ChunkIndex, original.TotalChunks),
                _ => ChunkMetadata.CreateDataChunk(sharedMessageId, original.ChunkIndex, original.TotalChunks),
            };
            ReplaceChunkMetadata(chunk, replacement.Format());
        }

        var buffer = new ChunkBuffer(new ChunkingOptions());
        ChunkBufferResult? firstResult = null;
        ChunkBufferResult? secondResult = null;
        for (int i = 0; i < first.Count; i++)
        {
            firstResult = buffer.AddChunk(Received(first[i]), Now, ExpiresAt);
            secondResult = buffer.AddChunk(Received(second[i]), Now, ExpiresAt);
        }

        Assert.Equal(firstPayload, firstResult!.ReassembledMessage!.ApplicationMessage.Payload.ToArray());
        Assert.Equal(secondPayload, secondResult!.ReassembledMessage!.ApplicationMessage.Payload.ToArray());
    }

    [Fact]
    public async Task AddChunk_OneMessageExpiresWhileAnotherReassembles()
    {
        var orphanAcknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var buffer = new ChunkBuffer(new ChunkingOptions());

        // A message that will stall, held with a nearer deadline than the one that completes.
        var stalled = Split(NewPayload(4096));
        var orphan = Received(stalled[0], () => orphanAcknowledged.TrySetResult());
        Assert.Empty(buffer.AddChunk(orphan, Now, Now.AddMilliseconds(50)).DiscardedChunks);

        // A second message completes independently while the first message's timer remains active.
        var completing = Split(NewPayload(4096));
        DateTime farDeadline = Now.AddSeconds(60);
        ChunkBufferResult? result = null;
        foreach (MqttApplicationMessage chunk in completing)
        {
            result = buffer.AddChunk(Received(chunk), Now, farDeadline);
        }

        Assert.NotNull(result!.ReassembledMessage);
        await orphanAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddChunk_ChunkWithoutMessageExpiry_IsDiscarded()
    {
        var chunks = Split(NewPayload(4096));
        var buffer = new ChunkBuffer(new ChunkingOptions());

        chunks[0].MessageExpiryInterval = 0;
        var args = Received(chunks[0]);

        var result = buffer.AddChunk(args, Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Same(args, Assert.Single(result.DiscardedChunks));
    }

    [Fact]
    public void AddChunk_CustomChecksum_RoundTripsWhenBothEndsKnowIt()
    {
        var checksum = new Fnv1a64ChunkChecksum();
        var payload = NewPayload(4096);

        var splitter = new ChunkedMessageSplitter(new ChunkingOptions
        {
            Checksum = checksum,
        });
        var chunks = splitter.SplitMessage(NewMessage(payload), TestMaxPacketSize);

        // The head chunk names the algorithm, so a receiver that knows it can verify.
        Assert.Contains($":{checksum.Id}:", ChunkValue(chunks[0]), StringComparison.Ordinal);

        var buffer = new ChunkBuffer(new ChunkingOptions
        {
            ResolveChecksum = id => id == checksum.Id ? checksum : null,
        });

        ChunkBufferResult? result = null;
        foreach (var chunk in chunks)
        {
            result = buffer.AddChunk(Received(chunk), Now, ExpiresAt);
        }

        Assert.NotNull(result!.ReassembledMessage);
        Assert.Equal(payload, result.ReassembledMessage!.ApplicationMessage.Payload.ToArray());
    }

    [Fact]
    public void AddChunk_ChecksumTheReceiverCannotResolve_DiscardsRatherThanMisverifying()
    {
        var splitter = new ChunkedMessageSplitter(new ChunkingOptions
        {
            Checksum = new Fnv1a64ChunkChecksum(),
        });
        var chunks = splitter.SplitMessage(NewMessage(NewPayload(4096)), TestMaxPacketSize);

        // A receiver knowing only the built-ins must refuse the message outright: verifying with
        // the wrong algorithm would look exactly like data corruption.
        var buffer = new ChunkBuffer(new ChunkingOptions());

        var head = Received(chunks[0]);
        var result = buffer.AddChunk(head, Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Same(head, Assert.Single(result.DiscardedChunks));
    }

    private sealed class Fnv1a64ChunkChecksum : IChunkChecksum
    {
        public string Id => "fnv1a64";

        public string Compute(ReadOnlySequence<byte> payload)
        {
            ulong hash = 14695981039346656037;
            foreach (ReadOnlyMemory<byte> segment in payload)
            {
                foreach (byte b in segment.Span)
                {
                    hash ^= b;
                    hash *= 1099511628211;
                }
            }

            return hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private static string? ChunkValue(MqttApplicationMessage message) =>
        message.UserProperties?.FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty)?.Value;

    private static ChunkMetadata Metadata(MqttApplicationMessage message)
    {
        Assert.True(ChunkMetadata.TryParse(ChunkValue(message), out ChunkMetadata? metadata));
        return metadata!;
    }

    private static void ReplaceChunkMetadata(MqttApplicationMessage message, string value)
    {
        List<MqttUserProperty> properties = message.UserProperties!;
        int index = properties.FindIndex(p => p.Name == ChunkingConstants.ChunkUserProperty);
        properties[index] = new MqttUserProperty(ChunkingConstants.ChunkUserProperty, value);
    }

    private static byte[] NewPayload(int size)
    {
        var payload = new byte[size];
        Random.Shared.NextBytes(payload);
        return payload;
    }

    private static IReadOnlyList<MqttApplicationMessage> Split(byte[] payload, bool extraProperty = false)
    {
        var splitter = new ChunkedMessageSplitter(new ChunkingOptions());
        return splitter.SplitMessage(NewMessage(payload, extraProperty), TestMaxPacketSize);
    }

    private static MqttApplicationMessage NewMessage(byte[] payload, bool extraProperty = false) =>
        new("test/topic")
        {
            Payload = new ReadOnlySequence<byte>(payload),
            ResponseTopic = "test/response",
            CorrelationData = Guid.NewGuid().ToByteArray(),
            MessageExpiryInterval = 10u,
            UserProperties = extraProperty ? [new MqttUserProperty("originalProperty", "value")] : null
        };

    private static MqttApplicationMessageReceivedEventArgs Received(
        MqttApplicationMessage message,
        Action? onAcknowledge = null,
        ushort packetIdentifier = 1)
    {
        return new MqttApplicationMessageReceivedEventArgs(
            "testClient",
            message,
            packetIdentifier,
            (_, _) =>
            {
                onAcknowledge?.Invoke();
                return Task.CompletedTask;
            });
    }
}
