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
