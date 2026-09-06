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
    public async Task ChunkedRequest_PropertyChunksCarryTheFullPropertySet()
    {
        IReadOnlyList<MqttApplicationMessage> chunks = await PublishChunkedRequestAsync();

        Assert.DoesNotContain(chunks[0].UserProperties!, p => p.Name == AkriSystemProperties.SourceId);
        Assert.Contains(
            chunks.Where(c => GetChunkMetadata(c).Kind == ChunkKind.Property)
                .SelectMany(c => c.UserProperties ?? []),
            p => p.Name == AkriSystemProperties.SourceId);

        foreach (MqttApplicationMessage chunk in chunks)
        {
            // Routing and per-chunk validation properties must survive on every chunk.
            Assert.Contains(chunk.UserProperties!, p => p.Name == "$partition");
            Assert.Contains(chunk.UserProperties!, p => p.Name == AkriSystemProperties.HighPriority);
            Assert.Contains(chunk.UserProperties!, p => p.Name == AkriSystemProperties.ProtocolVersion);
        }
    }

    private static ChunkMetadata GetChunkMetadata(MqttApplicationMessage message)
    {
        string? value = message.UserProperties?
            .FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty)?.Value;
        Assert.True(ChunkMetadata.TryParse(value, out ChunkMetadata? metadata));
        return metadata!;
    }

    [Fact]
    public async Task LargeResponse_IsSplitByExecutor()
    {
        MockMqttPubSubClient executorMock = new();
        IReadOnlyList<MqttApplicationMessage> requestChunks = await PublishChunkedRequestAsync();

        await using EchoExecutor executor = new(new ApplicationContext(), executorMock)
        {
            RequestTopicPattern = "mock/echo",
            OnCommandReceived = (request, ct) => Task.FromResult(new ExtendedResponse<string> { Response = LargePayload }),
        };
        await executor.StartAsync();

        foreach (MqttApplicationMessage chunk in requestChunks)
        {
            await executorMock.SimulateNewMessage(chunk);
        }

        await WaitForAllAcknowledgementsAsync(executorMock, requestChunks.Count);

        Assert.True(executorMock.MessagesPublished.Count > 1, $"Expected a chunked response, got {executorMock.MessagesPublished.Count} message(s).");
        Assert.All(executorMock.MessagesPublished, m => Assert.True(ChunkBuffer.IsChunk(m)));
    }

    [Fact]
    public async Task LargeResponse_ForLegacyRequester_ReturnsServiceUnavailableWithoutChunks()
    {
        MockMqttPubSubClient executorMock = new();
        await using EchoExecutor executor = new(new ApplicationContext(), executorMock)
        {
            RequestTopicPattern = "mock/echo",
            OnCommandReceived = (request, ct) => Task.FromResult(new ExtendedResponse<string> { Response = LargePayload }),
        };
        await executor.StartAsync();

        MqttApplicationMessage request = SmallRequestMessage();
        await executorMock.SimulateNewMessage(request);
        await executorMock.SimulatedMessageAcknowledged();

        MqttApplicationMessage response = Assert.Single(executorMock.MessagesPublished);
        Assert.False(ChunkBuffer.IsChunk(response));
        Assert.Contains(response.UserProperties!, p => p.Name == AkriSystemProperties.Status && p.Value == "503");
        Assert.Contains(response.UserProperties!, p => p.Name == AkriSystemProperties.ProtocolVersion && p.Value == "1.0");
    }

    [Fact]
    public async Task CachedLargeResponse_ReplayedToLegacyRequester_IsNotChunked()
    {
        MockMqttPubSubClient executorMock = new();
        int handlerCalls = 0;
        await using EchoExecutor executor = new(new ApplicationContext(), executorMock)
        {
            RequestTopicPattern = "mock/echo",
            IsIdempotent = true,
            CacheTtl = TimeSpan.FromMinutes(1),
            OnCommandReceived = (request, ct) =>
            {
                handlerCalls++;
                return Task.FromResult(new ExtendedResponse<string> { Response = LargePayload });
            },
        };
        await executor.StartAsync();

        IReadOnlyList<MqttApplicationMessage> requestChunks = await PublishChunkedRequestAsync();
        foreach (MqttApplicationMessage chunk in requestChunks)
        {
            await executorMock.SimulateNewMessage(chunk);
        }
        await WaitForAllAcknowledgementsAsync(executorMock, requestChunks.Count);
        Assert.True(executorMock.MessagesPublished.Count > 1);

        executorMock.MessagesPublished.Clear();
        MqttApplicationMessage legacyRequest = SmallRequestMessage();
        legacyRequest.Payload = new Utf8JsonSerializer().ToBytes(LargePayload).SerializedPayload;
        legacyRequest.ResponseTopic = requestChunks[0].ResponseTopic;
        legacyRequest.CorrelationData = requestChunks[0].CorrelationData;
        await executorMock.SimulateNewMessage(legacyRequest);
        await executorMock.SimulatedMessageAcknowledged();

        MqttApplicationMessage response = Assert.Single(executorMock.MessagesPublished);
        Assert.False(ChunkBuffer.IsChunk(response));
        Assert.Contains(response.UserProperties!, p => p.Name == AkriSystemProperties.Status && p.Value == "503");
        Assert.Equal(1, handlerCalls);
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

        await WaitForAllChunksAsync(invokerMock);

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
    public async Task SmallRequest_LargeResponse_IsChunkedAndReassembled()
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

        Task<ExtendedResponse<string>> invocation = invoker.InvokeCommandAsync(
            SmallPayload,
            commandTimeout: TimeSpan.FromSeconds(60));
        while (invokerMock.MessagesPublished.Count == 0)
        {
            await Task.Yield();
        }

        MqttApplicationMessage request = Assert.Single(invokerMock.MessagesPublished);
        Assert.False(ChunkBuffer.IsChunk(request));
        Assert.Contains(request.UserProperties!, property =>
            property.Name == AkriSystemProperties.ProtocolVersion && property.Value == "1.0");
        Assert.Contains(request.UserProperties!, property =>
            property.Name == AkriSystemProperties.SupportedMajorProtocolVersions && property.Value == "1 2");

        await executorMock.SimulateNewMessage(request);
        await executorMock.SimulatedMessageAcknowledged();
        Assert.True(executorMock.MessagesPublished.Count > 1);
        Assert.All(executorMock.MessagesPublished, responseChunk =>
        {
            Assert.True(ChunkBuffer.IsChunk(responseChunk));
            Assert.Contains(responseChunk.UserProperties!, property =>
                property.Name == AkriSystemProperties.ProtocolVersion && property.Value == "2.0");
        });

        foreach (MqttApplicationMessage responseChunk in executorMock.MessagesPublished)
        {
            await invokerMock.SimulateNewMessage(responseChunk);
        }

        ExtendedResponse<string> response = await invocation;
        Assert.Equal(LargePayload, response.Response);
    }

    [Fact]
    public async Task LargeRoundTrip_InvokerAndExecutorShareClient_AcknowledgesEachChunkOnce()
    {
        MockMqttPubSubClient sharedClient = new();
        await using EchoExecutor executor = new(new ApplicationContext(), sharedClient)
        {
            RequestTopicPattern = "mock/echo",
            OnCommandReceived = (request, ct) => Task.FromResult(new ExtendedResponse<string> { Response = LargePayload }),
        };
        await executor.StartAsync();
        await using EchoInvoker invoker = new(new ApplicationContext(), sharedClient) { RequestTopicPattern = "mock/echo" };

        Task<ExtendedResponse<string>> invocation = invoker.InvokeCommandAsync(
            LargePayload,
            commandTimeout: TimeSpan.FromSeconds(60));
        await WaitForAllChunksAsync(sharedClient);
        List<MqttApplicationMessage> requestChunks = [.. sharedClient.MessagesPublished];

        foreach (MqttApplicationMessage requestChunk in requestChunks)
        {
            await sharedClient.SimulateNewMessage(requestChunk);
        }
        await WaitForAllAcknowledgementsAsync(sharedClient, requestChunks.Count);

        List<MqttApplicationMessage> responseChunks = sharedClient.MessagesPublished
            .Skip(requestChunks.Count)
            .ToList();
        Assert.True(responseChunks.Count > 1);
        foreach (MqttApplicationMessage responseChunk in responseChunks)
        {
            await sharedClient.SimulateNewMessage(responseChunk);
        }

        ExtendedResponse<string> response = await invocation;
        Assert.Equal(LargePayload, response.Response);
        Assert.Equal(requestChunks.Count + responseChunks.Count, sharedClient.AcknowledgedMessageCount);
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

    [Fact]
    public async Task MalformedRequestChunk_IsAcknowledgedWithoutResponse()
    {
        MockMqttPubSubClient executorMock = new();
        await using EchoExecutor executor = new(new ApplicationContext(), executorMock)
        {
            RequestTopicPattern = "mock/echo",
            OnCommandReceived = (request, ct) => Task.FromResult(new ExtendedResponse<string> { Response = "unused" }),
        };
        await executor.StartAsync();
        MqttApplicationMessage request = SmallRequestMessage();
        request.AddUserProperty(AkriSystemProperties.ProtocolVersion, "2.0");
        request.AddUserProperty(ChunkingConstants.ChunkUserProperty, "invalid");

        await executorMock.SimulateNewMessage(request);
        await executorMock.SimulatedMessageAcknowledged();

        Assert.Empty(executorMock.MessagesPublished);
    }

    [Fact]
    public async Task RepeatedMalformedRequestChunks_AreBothAcknowledged()
    {
        MockMqttPubSubClient executorMock = new();
        await using EchoExecutor executor = new(new ApplicationContext(), executorMock)
        {
            RequestTopicPattern = "mock/echo",
            OnCommandReceived = (request, ct) => Task.FromResult(new ExtendedResponse<string> { Response = "unused" }),
        };
        await executor.StartAsync();
        MqttApplicationMessage request = SmallRequestMessage();
        request.AddUserProperty(AkriSystemProperties.ProtocolVersion, "2.0");
        request.AddUserProperty(ChunkingConstants.ChunkUserProperty, "invalid");

        await executorMock.SimulateNewMessage(request);
        await executorMock.SimulateNewMessage(request);
        await executorMock.SimulatedMessageAcknowledged();
        await executorMock.SimulatedMessageAcknowledged();

        Assert.Empty(executorMock.MessagesPublished);
        Assert.Equal(2, executorMock.AcknowledgedMessageCount);
    }

    [Fact]
    public async Task ChunkedRequestWithHigherMinorVersion_ResponseUsesImplementedMinorVersion()
    {
        IReadOnlyList<MqttApplicationMessage> chunks = await PublishChunkedRequestAsync();
        foreach (MqttApplicationMessage chunk in chunks)
        {
            List<MqttUserProperty> properties = chunk.UserProperties!;
            foreach (MqttUserProperty property in properties.Where(
                p => p.Name == AkriSystemProperties.ProtocolVersion).ToList())
            {
                int index = properties.IndexOf(property);
                properties[index] = new MqttUserProperty(AkriSystemProperties.ProtocolVersion, "2.3");
            }
        }

        MockMqttPubSubClient executorMock = new();
        await using EchoExecutor executor = new(new ApplicationContext(), executorMock)
        {
            RequestTopicPattern = "mock/echo",
            OnCommandReceived = (request, ct) => Task.FromResult(new ExtendedResponse<string> { Response = "ok" }),
        };
        await executor.StartAsync();
        foreach (MqttApplicationMessage chunk in chunks)
        {
            await executorMock.SimulateNewMessage(chunk);
        }
        await WaitForAllAcknowledgementsAsync(executorMock, chunks.Count);

        MqttApplicationMessage response = Assert.Single(executorMock.MessagesPublished);
        Assert.Contains(response.UserProperties!, p => p.Name == AkriSystemProperties.ProtocolVersion && p.Value == "2.0");
    }

    [Fact]
    public async Task MalformedResponseChunk_IsReleasedAndInvocationTimesOut()
    {
        MockMqttPubSubClient invokerMock = new();
        await using EchoInvoker invoker = new(new ApplicationContext(), invokerMock) { RequestTopicPattern = "mock/echo" };
        Task<ExtendedResponse<string>> invocation = invoker.InvokeCommandAsync(LargePayload, commandTimeout: TimeSpan.FromSeconds(3));
        MqttApplicationMessage request = invokerMock.MessagePublished;
        var response = new MqttApplicationMessage(request.ResponseTopic!)
        {
            CorrelationData = request.CorrelationData,
            MessageExpiryInterval = 10,
            UserProperties =
            [
                new(AkriSystemProperties.ProtocolVersion, "2.0"),
                new(ChunkingConstants.ChunkUserProperty, "invalid"),
            ],
        };

        await invokerMock.SimulateNewMessage(response);

        // The unusable delivery is released immediately; the caller fails on its own budget.
        await invokerMock.SimulatedMessageAcknowledged();

        AkriMqttException exception = await Assert.ThrowsAsync<AkriMqttException>(() => invocation);
        Assert.Equal(AkriMqttErrorKind.Timeout, exception.Kind);
    }

    [Fact]
    public async Task RequestRequiringTooManyChunks_FailsAsPayloadInvalid()
    {
        MockMqttPubSubClient mock = new();
        await using EchoInvoker invoker = new(new ApplicationContext(), mock) { RequestTopicPattern = "mock/echo" };
        string oversized = new('x', 7_000_000);

        AkriMqttException exception = await Assert.ThrowsAsync<AkriMqttException>(
            () => invoker.InvokeCommandAsync(oversized, commandTimeout: TimeSpan.FromSeconds(30)));

        Assert.Equal(AkriMqttErrorKind.PayloadInvalid, exception.Kind);
        Assert.Empty(mock.MessagesPublished);
    }

    [Fact]
    public async Task ChunkedRequestRejectedByLegacyExecutor_PreservesSupportedVersions()
    {
        MockMqttPubSubClient mock = new();
        await using EchoInvoker invoker = new(new ApplicationContext(), mock) { RequestTopicPattern = "mock/echo" };
        Task<ExtendedResponse<string>> invocation = invoker.InvokeCommandAsync(
            LargePayload,
            commandTimeout: TimeSpan.FromSeconds(10));
        await WaitForAllChunksAsync(mock);
        MqttApplicationMessage requestChunk = mock.MessagePublished;
        var rejection = new MqttApplicationMessage(requestChunk.ResponseTopic!)
        {
            CorrelationData = requestChunk.CorrelationData,
            MessageExpiryInterval = 10,
            UserProperties =
            [
                new(AkriSystemProperties.ProtocolVersion, "1.0"),
                new(AkriSystemProperties.Status, "505"),
                new(AkriSystemProperties.RequestedProtocolVersion, "2.0"),
                new(AkriSystemProperties.SupportedMajorProtocolVersions, "1"),
            ],
        };

        await mock.SimulateNewMessage(rejection);

        AkriMqttException exception = await Assert.ThrowsAsync<AkriMqttException>(() => invocation);
        Assert.Equal(AkriMqttErrorKind.UnsupportedVersion, exception.Kind);
        Assert.True(exception.IsRemote);
        Assert.Equal("2.0", exception.ProtocolVersion);
        Assert.Equal(1, Assert.Single(exception.SupportedMajorProtocolVersions!));
    }

    [Fact]
    public async Task ChunkWithLegacyVersion_ExecutorAdvertisesOrdinaryAndChunkingMajors()
    {
        IReadOnlyList<MqttApplicationMessage> chunks = await PublishChunkedRequestAsync();
        foreach (MqttApplicationMessage chunk in chunks)
        {
            List<MqttUserProperty> properties = chunk.UserProperties!;
            foreach (MqttUserProperty version in properties.Where(
                property => property.Name == AkriSystemProperties.ProtocolVersion).ToList())
            {
                properties[properties.IndexOf(version)] = new MqttUserProperty(
                    AkriSystemProperties.ProtocolVersion,
                    "1.0");
            }
        }

        MockMqttPubSubClient executorMock = new();
        await using EchoExecutor executor = new(new ApplicationContext(), executorMock)
        {
            RequestTopicPattern = "mock/echo",
            OnCommandReceived = (request, ct) => Task.FromResult(new ExtendedResponse<string> { Response = "unused" }),
        };
        await executor.StartAsync();

        await executorMock.SimulateNewMessage(chunks[0]);
        await executorMock.SimulatedMessageAcknowledged();

        MqttApplicationMessage response = Assert.Single(executorMock.MessagesPublished);
        Assert.Contains(response.UserProperties!, property =>
            property.Name == AkriSystemProperties.Status && property.Value == "505");
        Assert.Contains(response.UserProperties!, property =>
            property.Name == AkriSystemProperties.SupportedMajorProtocolVersions && property.Value == "1 2");
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

    // The invoker publishes from an async method, so the chunks are not guaranteed to have landed
    // by the time InvokeCommandAsync yields. Every chunk states how many there will be, so wait for
    // the first one and then for the rest rather than predicting the count.
    private static async Task WaitForAllChunksAsync(MockMqttPubSubClient mock)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));

        while (true)
        {
            MqttApplicationMessage? head = mock.MessagesPublished.FirstOrDefault();
            string? chunkValue = head?.UserProperties?
                .FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty)?.Value;

            if (ChunkMetadata.TryParse(chunkValue, out ChunkMetadata? metadata)
                && mock.MessagesPublished.Count >= metadata!.TotalChunks)
            {
                return;
            }

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
            List<MqttUserProperty> properties = chunk.UserProperties!;
            int metadataIndex = properties.FindIndex(p => p.Name == ChunkingConstants.ChunkUserProperty);
            Assert.True(ChunkMetadata.TryParse(properties[metadataIndex].Value, out ChunkMetadata? chunkMetadata));
            properties[metadataIndex] = new MqttUserProperty(
                ChunkingConstants.ChunkUserProperty,
                chunkMetadata!.Format(30));
        }

        return chunks;
    }
}
