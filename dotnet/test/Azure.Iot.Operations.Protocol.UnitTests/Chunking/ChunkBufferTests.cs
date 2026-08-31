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
    private const int TestSafetyMargin = 16;

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
    public void Split_ProducesAHeaderChunkCarryingNoPayload()
    {
        var chunks = Split(NewPayload(4096), extraProperty: true);

        Assert.True(chunks.Count > 2);
        Assert.Equal(0, chunks[0].Payload.Length);
        Assert.All(chunks.Skip(1), c => Assert.True(c.Payload.Length > 0));

        // The header chunk is the one carrying the user's properties.
        Assert.Contains(chunks[0].UserProperties!, p => p.Name == "originalProperty");
        Assert.All(chunks.Skip(1), c => Assert.DoesNotContain(c.UserProperties!, p => p.Name == "originalProperty"));
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
        var chunks = Split(payload);
        var buffer = new ChunkBuffer(new ChunkingOptions());

        ChunkBufferResult? result = null;
        foreach (var chunk in chunks.Reverse())
        {
            result = buffer.AddChunk(Received(chunk), Now, ExpiresAt);
        }

        Assert.NotNull(result!.ReassembledMessage);
        Assert.Equal(payload, result.ReassembledMessage!.ApplicationMessage.Payload.ToArray());
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
    public async Task AddChunk_RedeliveredChunk_HoldsTheNewDeliveryAndAcknowledgesNeitherEarly()
    {
        var chunks = Split(NewPayload(4096));
        var buffer = new ChunkBuffer(new ChunkingOptions());
        var acknowledged = new List<string>();

        Assert.True(chunks.Count > 2);

        buffer.AddChunk(Received(chunks[0], () => acknowledged.Add("0")), Now, ExpiresAt);
        buffer.AddChunk(Received(chunks[1], () => acknowledged.Add("1-first")), Now, ExpiresAt);

        var redelivery = buffer.AddChunk(Received(chunks[1], () => acknowledged.Add("1-redelivered")), Now, ExpiresAt);

        Assert.Null(redelivery.ReassembledMessage);
        Assert.Empty(redelivery.DiscardedChunks);
        Assert.Empty(acknowledged);

        ChunkBufferResult? result = null;
        for (int i = 2; i < chunks.Count; i++)
        {
            int index = i;
            result = buffer.AddChunk(Received(chunks[i], () => acknowledged.Add(index.ToString())), Now, ExpiresAt);
        }

        await result!.ReassembledMessage!.AcknowledgeAsync(CancellationToken.None);

        Assert.Contains("1-redelivered", acknowledged);
        Assert.DoesNotContain("1-first", acknowledged);
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
    public void AddChunk_DuplicateChunk_ReplacesTheHeldDeliveryRatherThanAcknowledgingIt()
    {
        var chunks = Split(NewPayload(4096));
        var buffer = new ChunkBuffer(new ChunkingOptions());

        Assert.Empty(buffer.AddChunk(Received(chunks[0]), Now, ExpiresAt).DiscardedChunks);

        var redelivery = Received(chunks[0]);
        var result = buffer.AddChunk(redelivery, Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Empty(result.DiscardedChunks);
    }

    [Fact]
    public void AddChunk_TooManyChunks_DiscardsMessage()
    {
        var chunks = Split(NewPayload(4096));
        var buffer = new ChunkBuffer(new ChunkingOptions { MaxChunkCount = 2 });

        Assert.True(chunks.Count > 2);

        var result = buffer.AddChunk(Received(chunks[0]), Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Single(result.DiscardedChunks);
    }

    [Fact]
    public void AddChunk_ExceedsReassemblyBufferLimit_DiscardsMessageAndReleasesHeldChunks()
    {
        var chunks = Split(NewPayload(4096));

        // Room for the header chunk, which carries no payload, and one data chunk but not two.
        long limit = chunks[1].Payload.Length + 1;
        var buffer = new ChunkBuffer(new ChunkingOptions { ReassemblyBufferSizeLimit = limit });

        var header = Received(chunks[0]);
        Assert.Empty(buffer.AddChunk(header, Now, ExpiresAt).DiscardedChunks);

        var first = Received(chunks[1]);
        Assert.Empty(buffer.AddChunk(first, Now, ExpiresAt).DiscardedChunks);

        var second = Received(chunks[2]);
        var result = buffer.AddChunk(second, Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Equal([header, first, second], result.DiscardedChunks);
    }

    [Fact]
    public void AddChunk_PartialMessageExpires_AbandonsAndReturnsHeldChunks()
    {
        var chunks = Split(NewPayload(4096));
        var buffer = new ChunkBuffer(new ChunkingOptions());

        var held = Received(chunks[0]);
        Assert.Empty(buffer.AddChunk(held, Now, ExpiresAt).DiscardedChunks);

        // A later, unrelated chunk arrives after the first message's deadline.
        var laterChunks = Split(NewPayload(4096));
        var afterExpiry = ExpiresAt.AddSeconds(1);
        var result = buffer.AddChunk(Received(laterChunks[0]), afterExpiry, afterExpiry.AddSeconds(10));

        Assert.Null(result.ReassembledMessage);
        Assert.Contains(held, result.DiscardedChunks);
    }

    [Fact]
    public void AddChunk_ExpirySweepCoincidesWithReassembly_StillReturnsTheAbandonedChunks()
    {
        var buffer = new ChunkBuffer(new ChunkingOptions());

        // A message that will stall, held with a nearer deadline than the one that completes.
        var stalled = Split(NewPayload(4096));
        var orphan = Received(stalled[0]);
        Assert.Empty(buffer.AddChunk(orphan, Now, Now.AddSeconds(10)).DiscardedChunks);

        // A second message, all but its final chunk arriving before the first message expires.
        var completing = Split(NewPayload(4096));
        DateTime farDeadline = Now.AddSeconds(60);
        for (int i = 0; i < completing.Count - 1; i++)
        {
            Assert.Empty(buffer.AddChunk(Received(completing[i]), Now, farDeadline).DiscardedChunks);
        }

        // The final chunk both sweeps the stalled message and completes this one.
        var result = buffer.AddChunk(Received(completing[^1]), Now.AddSeconds(11), farDeadline);

        Assert.NotNull(result.ReassembledMessage);
        Assert.Contains(orphan, result.DiscardedChunks);
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
            StaticOverhead = TestSafetyMargin,
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
            StaticOverhead = TestSafetyMargin,
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

    private static byte[] NewPayload(int size)
    {
        var payload = new byte[size];
        Random.Shared.NextBytes(payload);
        return payload;
    }

    private static IReadOnlyList<MqttApplicationMessage> Split(byte[] payload, bool extraProperty = false)
    {
        var splitter = new ChunkedMessageSplitter(new ChunkingOptions { StaticOverhead = TestSafetyMargin });
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

    private static MqttApplicationMessageReceivedEventArgs Received(MqttApplicationMessage message, Action? onAcknowledge = null)
    {
        return new MqttApplicationMessageReceivedEventArgs(
            "testClient",
            message,
            1,
            (_, _) =>
            {
                onAcknowledge?.Invoke();
                return Task.CompletedTask;
            });
    }
}
