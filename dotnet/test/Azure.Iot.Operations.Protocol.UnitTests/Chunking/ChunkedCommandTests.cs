// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Chunking;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;

namespace Azure.Iot.Operations.Protocol.UnitTests.Chunking;

/// <summary>
/// End-to-end coverage for the chunking POC: the invoker splits an oversized request and the
/// executor reassembles it, with no change to either public API.
/// </summary>
public class ChunkedCommandTests
{
    private sealed class EchoInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
        : CommandInvoker<string, string>(applicationContext, mqttClient, "echo", new Utf8JsonSerializer());

    private sealed class EchoExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
        : CommandExecutor<string, string>(applicationContext, mqttClient, "echo", new Utf8JsonSerializer());

    // Comfortably above PlaceholderMaxPacketSize so the payload is split several ways.
    private static readonly string LargePayload = new('x', 200_000);

    private static readonly string SmallPayload = "small";

    [Fact]
    public async Task LargeRequest_IsSplitIntoChunks()
    {
        MockMqttPubSubClient mock = new();
        await using EchoInvoker invoker = new(new ApplicationContext(), mock) { RequestTopicPattern = "mock/echo" };

        // No executor is listening, so the invocation times out once the chunks have been published.
        await Assert.ThrowsAsync<AkriMqttException>(
            () => invoker.InvokeCommandAsync(LargePayload, commandTimeout: TimeSpan.FromSeconds(1)));

        Assert.True(mock.MessagesPublished.Count > 1, $"Expected multiple chunks, got {mock.MessagesPublished.Count}.");
        Assert.All(mock.MessagesPublished, m => Assert.True(ChunkBuffer.IsChunk(m)));
    }

    [Fact]
    public async Task SmallRequest_IsNotChunked()
    {
        MockMqttPubSubClient mock = new();
        await using EchoInvoker invoker = new(new ApplicationContext(), mock) { RequestTopicPattern = "mock/echo" };

        await Assert.ThrowsAsync<AkriMqttException>(
            () => invoker.InvokeCommandAsync(SmallPayload, commandTimeout: TimeSpan.FromSeconds(1)));

        Assert.Single(mock.MessagesPublished);
        Assert.False(ChunkBuffer.IsChunk(mock.MessagesPublished[0]));
    }

    [Fact]
    public async Task ChunkedRequest_IsReassembledByExecutor()
    {
        IReadOnlyList<MqttApplicationMessage> chunks = await PublishChunkedRequestAsync();

        MockMqttPubSubClient executorMock = new();
        string? handlerSaw = null;

        await using EchoExecutor executor = new(new ApplicationContext(), executorMock)
        {
            RequestTopicPattern = "mock/echo",
            OnCommandReceived = (request, ct) =>
            {
                handlerSaw = request.Request;
                return Task.FromResult(new ExtendedResponse<string> { Response = "ack" });
            },
        };
        await executor.StartAsync();

        foreach (MqttApplicationMessage chunk in chunks)
        {
            await executorMock.SimulateNewMessage(chunk);
        }

        await WaitForAllAcknowledgementsAsync(executorMock, chunks.Count);

        Assert.Equal(LargePayload, handlerSaw);
        Assert.Equal(chunks.Count, executorMock.AcknowledgedMessageCount);
    }

    [Fact]
    public async Task ChunkedRequest_ExecutorRunsHandlerOnceAndPublishesOneResponse()
    {
        IReadOnlyList<MqttApplicationMessage> chunks = await PublishChunkedRequestAsync();

        MockMqttPubSubClient executorMock = new();
        int handlerInvocations = 0;

        await using EchoExecutor executor = new(new ApplicationContext(), executorMock)
        {
            RequestTopicPattern = "mock/echo",
            OnCommandReceived = (request, ct) =>
            {
                Interlocked.Increment(ref handlerInvocations);
                return Task.FromResult(new ExtendedResponse<string> { Response = "ack" });
            },
        };
        await executor.StartAsync();

        foreach (MqttApplicationMessage chunk in chunks)
        {
            await executorMock.SimulateNewMessage(chunk);
        }

        await WaitForAllAcknowledgementsAsync(executorMock, chunks.Count);

        Assert.Equal(1, handlerInvocations);
        Assert.Single(executorMock.MessagesPublished);
    }

    // The executor dispatches handlers onto the thread pool, so acknowledgement is the only
    // observable signal that a request has been fully processed.
    private static async Task WaitForAllAcknowledgementsAsync(MockMqttPubSubClient mock, int chunkCount)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));

        for (int i = 0; i < chunkCount; i++)
        {
            await mock.SimulatedMessageAcknowledged(timeout.Token);
        }
    }

    [Fact]
    public async Task ChunkedRequest_OnlyFirstChunkCarriesTheFullPropertySet()
    {
        IReadOnlyList<MqttApplicationMessage> chunks = await PublishChunkedRequestAsync();

        Assert.Contains(chunks[0].UserProperties!, p => p.Name == AkriSystemProperties.SourceId);

        foreach (MqttApplicationMessage chunk in chunks.Skip(1))
        {
            Assert.DoesNotContain(chunk.UserProperties!, p => p.Name == AkriSystemProperties.SourceId);

            // Routing and per-chunk validation properties must survive on every chunk.
            Assert.Contains(chunk.UserProperties!, p => p.Name == "$partition");
            Assert.Contains(chunk.UserProperties!, p => p.Name == AkriSystemProperties.HighPriority);
            Assert.Contains(chunk.UserProperties!, p => p.Name == AkriSystemProperties.ProtocolVersion);
        }
    }

    [Fact]
    public async Task LargeResponse_IsSplitByExecutor()
    {
        MockMqttPubSubClient executorMock = new();

        await using EchoExecutor executor = new(new ApplicationContext(), executorMock)
        {
            RequestTopicPattern = "mock/echo",
            OnCommandReceived = (request, ct) => Task.FromResult(new ExtendedResponse<string> { Response = LargePayload }),
        };
        await executor.StartAsync();

        await executorMock.SimulateNewMessage(SmallRequestMessage());
        await executorMock.SimulatedMessageAcknowledged();

        Assert.True(executorMock.MessagesPublished.Count > 1, $"Expected a chunked response, got {executorMock.MessagesPublished.Count} message(s).");
        Assert.All(executorMock.MessagesPublished, m => Assert.True(ChunkBuffer.IsChunk(m)));
    }

    [Fact]
    public async Task LargeResponse_IsReassembledByInvoker()
    {
        MockMqttPubSubClient invokerMock = new();
        MockMqttPubSubClient executorMock = new();

        await using EchoExecutor executor = new(new ApplicationContext(), executorMock)
        {
            RequestTopicPattern = "mock/echo",
            OnCommandReceived = (request, ct) => Task.FromResult(new ExtendedResponse<string> { Response = LargePayload }),
        };
        await executor.StartAsync();

        await using EchoInvoker invoker = new(new ApplicationContext(), invokerMock) { RequestTopicPattern = "mock/echo" };

        Task<ExtendedResponse<string>> invocation =
            invoker.InvokeCommandAsync(LargePayload, commandTimeout: TimeSpan.FromSeconds(60));

        int expectedRequestChunks = ExpectedChunkCount(LargePayload);
        await WaitForPublishesAsync(invokerMock, expectedRequestChunks);

        List<MqttApplicationMessage> requestChunks = [.. invokerMock.MessagesPublished];
        foreach (MqttApplicationMessage chunk in requestChunks)
        {
            await executorMock.SimulateNewMessage(chunk);
        }

        await WaitForAllAcknowledgementsAsync(executorMock, requestChunks.Count);

        foreach (MqttApplicationMessage responseChunk in executorMock.MessagesPublished)
        {
            await invokerMock.SimulateNewMessage(responseChunk);
        }

        ExtendedResponse<string> response = await invocation;

        Assert.Equal(LargePayload, response.Response);
        Assert.True(executorMock.MessagesPublished.Count > 1, "Expected the response to have been chunked.");
    }

    [Fact]
    public async Task ChunkedRequest_EveryChunkCarriesAPositiveExpiryWithinTheInvocationBudget()
    {
        MockMqttPubSubClient mock = new();
        await using EchoInvoker invoker = new(new ApplicationContext(), mock) { RequestTopicPattern = "mock/echo" };

        TimeSpan commandTimeout = TimeSpan.FromSeconds(10);

        await Assert.ThrowsAsync<AkriMqttException>(
            () => invoker.InvokeCommandAsync(LargePayload, commandTimeout: commandTimeout));

        Assert.True(mock.MessagesPublished.Count > 1);

        foreach (MqttApplicationMessage chunk in mock.MessagesPublished)
        {
            // Zero means "already expired" to the receiving envoy, so it must never go on the wire.
            Assert.True(chunk.MessageExpiryInterval > 0);

            // No chunk may outlive the invocation it belongs to.
            Assert.True(chunk.MessageExpiryInterval <= (uint)commandTimeout.TotalSeconds);
        }
    }

    private static MqttApplicationMessage SmallRequestMessage()
    {
        Utf8JsonSerializer serializer = new();
        SerializedPayloadContext payload = serializer.ToBytes(SmallPayload);

        MqttApplicationMessage request = new("mock/echo")
        {
            Payload = payload.SerializedPayload,
            ContentType = payload.ContentType,
            PayloadFormatIndicator = (MqttPayloadFormatIndicator)payload.PayloadFormatIndicator,
            CorrelationData = Guid.NewGuid().ToByteArray(),
            MessageExpiryInterval = 30,
            ResponseTopic = "mock/echo/response",
        };

        request.AddUserProperty(AkriSystemProperties.SourceId, Guid.NewGuid().ToString());
        return request;
    }

    private static int ExpectedChunkCount(string payload)
    {
        long serializedLength = new Utf8JsonSerializer().ToBytes(payload).SerializedPayload.Length;
        int maxChunkSize = Utils.GetMaxChunkSize(ChunkingConstants.PlaceholderMaxPacketSize, new ChunkingOptions().StaticOverhead);

        return (int)Math.Ceiling(serializedLength / (double)maxChunkSize);
    }

    // The invoker publishes from an async method, so the chunks are not guaranteed to have landed
    // by the time InvokeCommandAsync yields.
    private static async Task WaitForPublishesAsync(MockMqttPubSubClient mock, int expectedCount)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));

        while (mock.MessagesPublished.Count < expectedCount)
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static async Task<IReadOnlyList<MqttApplicationMessage>> PublishChunkedRequestAsync()
    {
        MockMqttPubSubClient invokerMock = new();
        await using EchoInvoker invoker = new(new ApplicationContext(), invokerMock) { RequestTopicPattern = "mock/echo" };

        await Assert.ThrowsAsync<AkriMqttException>(
            () => invoker.InvokeCommandAsync(LargePayload, commandTimeout: TimeSpan.FromSeconds(1)));

        List<MqttApplicationMessage> chunks = [.. invokerMock.MessagesPublished];

        // The short timeout above exists only so the harvesting invocation terminates promptly; it
        // would otherwise leave the chunks already expired by the time an executor sees them.
        foreach (MqttApplicationMessage chunk in chunks)
        {
            chunk.MessageExpiryInterval = 30;
        }

        return chunks;
    }
}
