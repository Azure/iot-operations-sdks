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
        var chunks = Split("payload that will need chunking"u8.ToArray(), maxPacketSize: 138, staticOverhead: 128);

        Assert.True(ChunkBuffer.IsChunk(chunks[0]));
    }

    [Fact]
    public void AddChunk_AllChunksInOrder_ReassemblesPayload()
    {
        var payload = NewPayload(4096);
        var chunks = Split(payload, maxPacketSize: 1124, staticOverhead: 1024);
        var buffer = new ChunkBuffer(new ChunkingOptions());

        Assert.True(chunks.Count > 1);

        for (int i = 0; i < chunks.Count - 1; i++)
        {
            var partial = buffer.AddChunk(Received(chunks[i]), Now, ExpiresAt);

            Assert.Null(partial.ReassembledMessage);
            Assert.Empty(partial.ToAcknowledge);
        }

        var result = buffer.AddChunk(Received(chunks[^1]), Now, ExpiresAt);

        Assert.NotNull(result.ReassembledMessage);
        Assert.Empty(result.ToAcknowledge);
        Assert.Equal(payload, result.ReassembledMessage!.ApplicationMessage.Payload.ToArray());
    }

    [Fact]
    public void AddChunk_ChunksOutOfOrder_ReassemblesPayload()
    {
        var payload = NewPayload(4096);
        var chunks = Split(payload, maxPacketSize: 1124, staticOverhead: 1024);
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
        var chunks = Split(NewPayload(4096), maxPacketSize: 1124, staticOverhead: 1024, extraProperty: true);
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
        var chunks = Split(NewPayload(4096), maxPacketSize: 1124, staticOverhead: 1024);
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
    public void AddChunk_UnparsableMetadata_DiscardsAndReturnsChunkForAcknowledgement()
    {
        var buffer = new ChunkBuffer(new ChunkingOptions());
        var message = new MqttApplicationMessage("test/topic")
        {
            Payload = new ReadOnlySequence<byte>(NewPayload(16)),
            UserProperties = [new MqttUserProperty(ChunkingConstants.ChunkUserProperty, "not-valid-metadata")]
        };
        var args = Received(message);

        var result = buffer.AddChunk(args, Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Same(args, Assert.Single(result.ToAcknowledge));
    }

    [Fact]
    public void AddChunk_DuplicateChunk_DiscardsTheRedeliveryOnly()
    {
        var chunks = Split(NewPayload(4096), maxPacketSize: 1124, staticOverhead: 1024);
        var buffer = new ChunkBuffer(new ChunkingOptions());

        Assert.Empty(buffer.AddChunk(Received(chunks[0]), Now, ExpiresAt).ToAcknowledge);

        var redelivery = Received(chunks[0]);
        var result = buffer.AddChunk(redelivery, Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Same(redelivery, Assert.Single(result.ToAcknowledge));
    }

    [Fact]
    public void AddChunk_TooManyChunks_DiscardsMessage()
    {
        var chunks = Split(NewPayload(4096), maxPacketSize: 1124, staticOverhead: 1024);
        var buffer = new ChunkBuffer(new ChunkingOptions { MaxChunkCount = 2 });

        Assert.True(chunks.Count > 2);

        var result = buffer.AddChunk(Received(chunks[0]), Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Single(result.ToAcknowledge);
    }

    [Fact]
    public void AddChunk_ExceedsReassemblyBufferLimit_DiscardsMessageAndReleasesHeldChunks()
    {
        // Chunk payloads are maxPacketSize - staticOverhead = 100 bytes, so the second chunk
        // is the one that crosses a 150 byte limit.
        var chunks = Split(NewPayload(4096), maxPacketSize: 1124, staticOverhead: 1024);
        var buffer = new ChunkBuffer(new ChunkingOptions { ReassemblyBufferSizeLimit = 150 });

        var first = Received(chunks[0]);
        Assert.Empty(buffer.AddChunk(first, Now, ExpiresAt).ToAcknowledge);

        var second = Received(chunks[1]);
        var result = buffer.AddChunk(second, Now, ExpiresAt);

        Assert.Null(result.ReassembledMessage);
        Assert.Equal([first, second], result.ToAcknowledge);
    }

    [Fact]
    public void AddChunk_PartialMessageExpires_AbandonsAndReturnsHeldChunks()
    {
        var chunks = Split(NewPayload(4096), maxPacketSize: 1124, staticOverhead: 1024);
        var buffer = new ChunkBuffer(new ChunkingOptions());

        var held = Received(chunks[0]);
        Assert.Empty(buffer.AddChunk(held, Now, ExpiresAt).ToAcknowledge);

        // A later, unrelated chunk arrives after the first message's deadline.
        var laterChunks = Split(NewPayload(4096), maxPacketSize: 1124, staticOverhead: 1024);
        var afterExpiry = ExpiresAt.AddSeconds(1);
        var result = buffer.AddChunk(Received(laterChunks[0]), afterExpiry, afterExpiry.AddSeconds(10));

        Assert.Null(result.ReassembledMessage);
        Assert.Contains(held, result.ToAcknowledge);
    }

    private static byte[] NewPayload(int size)
    {
        var payload = new byte[size];
        Random.Shared.NextBytes(payload);
        return payload;
    }

    private static IReadOnlyList<MqttApplicationMessage> Split(
        byte[] payload,
        int maxPacketSize,
        int staticOverhead,
        bool extraProperty = false)
    {
        var splitter = new ChunkedMessageSplitter(new ChunkingOptions { StaticOverhead = staticOverhead });
        var message = new MqttApplicationMessage("test/topic")
        {
            Payload = new ReadOnlySequence<byte>(payload),
            ResponseTopic = "test/response",
            CorrelationData = Guid.NewGuid().ToByteArray(),
            MessageExpiryInterval = 10u,
            UserProperties = extraProperty ? [new MqttUserProperty("originalProperty", "value")] : null
        };

        return splitter.SplitMessage(message, maxPacketSize);
    }

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
