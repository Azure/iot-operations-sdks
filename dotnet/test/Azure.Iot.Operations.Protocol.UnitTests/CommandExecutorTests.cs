// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class EchoCommandExecutor : CommandExecutor<string, string>
    {
        public EchoCommandExecutor(IMqttPubSubClient mqttClient, string commandName = "echo")
            : base(mqttClient, commandName, new Utf8JsonSerializer())
        {

        }
    }

    public class EchoWithMetadataCommandExecutor : CommandExecutor<string, string>
    {
        public EchoWithMetadataCommandExecutor(IMqttPubSubClient mqttClient)
            : base(mqttClient, "echoWithMetadata", new Utf8JsonSerializer())
        {

        }
    }

    public class DelayCommandExecutor : CommandExecutor<TimeSpanClass, IntegerClass>
    {
        public DelayCommandExecutor(IMqttPubSubClient mqttClient, string commandName = "delay")
            : base(mqttClient, commandName, new Utf8JsonSerializer())
        {

        }
    }

    public sealed class TimeSpanClass
    {
        public TimeSpan TimeSpan { get; set; }
    }

    public sealed class IntegerClass
    {
        public int Integer { get; set; }
    }

    public class CommandExecutorTests
    {
        [Fact]
        public async Task MqttProtocolVersionUnknownThrowsException()
        {
            MockMqttPubSubClient mock = new(protocolVersion: MqttProtocolVersion.Unknown);
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("MQTTClient.ProtocolVersion", exception.PropertyName);
            Assert.Equal(MqttProtocolVersion.Unknown, exception.PropertyValue);
        }

        [Fact]
        public async Task MqttProtocolVersion310ThrowsException()
        {
            MockMqttPubSubClient mock = new(protocolVersion: MqttProtocolVersion.V310);
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("MQTTClient.ProtocolVersion", exception.PropertyName);
            Assert.Equal(MqttProtocolVersion.V310, exception.PropertyValue);
        }

        [Fact]
        public async Task MqttProtocolVersion311ThrowsException()
        {
            MockMqttPubSubClient mock = new(protocolVersion: MqttProtocolVersion.V311);
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("MQTTClient.ProtocolVersion", exception.PropertyName);
            Assert.Equal(MqttProtocolVersion.V311, exception.PropertyValue);
        }

        [Fact]
        public async Task InvalidRequestTopicPatternThrowsException()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/{improper/token}/echo",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("RequestTopicPattern", exception.PropertyName);
            Assert.Equal("mock/{improper/token}/echo", exception.PropertyValue);
        }

        [Fact]
        public async Task NonIdempotentCommandNegativeCacheTtlThrowsException()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                IsIdempotent = false,
                CacheTtl = TimeSpan.FromSeconds(-1),
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("CacheTtl", exception.PropertyName);
            Assert.Equal(TimeSpan.FromSeconds(-1), exception.PropertyValue);
        }

        [Fact]
        public async Task IdempotentCommandNegativeCacheTtlThrowsException()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                IsIdempotent = true,
                CacheTtl = TimeSpan.FromSeconds(-1),
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("CacheTtl", exception.PropertyName);
            Assert.Equal(TimeSpan.FromSeconds(-1), exception.PropertyValue);
        }

        [Fact]
        public async Task ExecutorNegativeTimeout_ThrowsException()
        {
            MockMqttPubSubClient mock = new();
            TaskCompletionSource tcs = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    tcs.SetResult();
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request
                    });
                },
                ExecutionTimeout = TimeSpan.FromSeconds(-1),
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("ExecutionTimeout", exception.PropertyName);
            Assert.Equal(TimeSpan.FromSeconds(-1), exception.PropertyValue);
        }

        [Fact]
        public async Task DuplicateRequest_NotIdempotent_WithinCommandTimeout_SameInvokerId_TopicContainsExecutorId_RetrievedFromCache()
        {
            MockMqttPubSubClient mock = new();
            string execClientId = mock.ClientId;
            int timesCmdExecuted = 0;

            await using EchoWithMetadataCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = $"mock/{execClientId}/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    int executionIndex = Interlocked.Increment(ref timesCmdExecuted);
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request + executionIndex,
                    });
                },
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            string invClientId1 = Guid.NewGuid().ToString();
            string payload = nameof(DuplicateRequest_NotIdempotent_WithinCommandTimeout_SameInvokerId_TopicContainsExecutorId_RetrievedFromCache);
            var serializer = new Utf8JsonSerializer();

            var payloadContext = serializer.ToBytes(payload);
            MqttApplicationMessage requestMsg = new MqttApplicationMessage($"mock/{execClientId}/echo")
            {
                PayloadSegment = payloadContext.SerializedPayload ?? Array.Empty<byte>(),
                ContentType = payloadContext.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext.PayloadFormatIndicator,
                MessageExpiryInterval = 10,
                ResponseTopic = $"mock/{execClientId}/echo/response",
            };

            requestMsg.AddUserProperty(AkriSystemProperties.SourceId, invClientId1);

            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulatedMessageAcknowledged();
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(2, mock.AcknowledgedMessageCount);
            Assert.Equal($"mock/{execClientId}/echo/response", mock.MessagePublished.Topic);
            Assert.Equal(serializer.ToBytes(payload + payload + 1).SerializedPayload, mock.MessagePublished.PayloadSegment.Array);
        }

        [Fact]
        public async Task DuplicateRequest_NotIdempotent_WithinCommandTimeout_NoInvokerId_TopicContainsExecutorId_RetrievedFromCache()
        {
            MockMqttPubSubClient mock = new();
            string execClientId = mock.ClientId;
            int timesCmdExecuted = 0;

            await using EchoWithMetadataCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = $"mock/{execClientId}/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    int executionIndex = Interlocked.Increment(ref timesCmdExecuted);
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request + executionIndex,
                    });
                },
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            string payload = nameof(DuplicateRequest_NotIdempotent_WithinCommandTimeout_NoInvokerId_TopicContainsExecutorId_RetrievedFromCache);
            var serializer = new Utf8JsonSerializer();
            var payloadContext = serializer.ToBytes(payload);
            MqttApplicationMessage requestMsg = new MqttApplicationMessage($"mock/{execClientId}/echo")
            {
                PayloadSegment = payloadContext.SerializedPayload ?? Array.Empty<byte>(),
                ContentType = payloadContext.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext.PayloadFormatIndicator,
                MessageExpiryInterval = 10,
                ResponseTopic = $"mock/{execClientId}/echo/response",
            };

            requestMsg.AddUserProperty(AkriSystemProperties.SourceId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulatedMessageAcknowledged();
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(2, mock.AcknowledgedMessageCount);
            Assert.Equal($"mock/{execClientId}/echo/response", mock.MessagePublished.Topic);
            Assert.Equal(serializer.ToBytes(payload + payload + 1).SerializedPayload, mock.MessagePublished.PayloadSegment.Array);
        }

        [Fact(Skip = "flaky")]
        public async Task DuplicateRequest_NotIdempotent_WithinCommandTimeout_DifferentInvokerId_TopicContainsExecutorId_NotRetrievedFromCache()
        {
            MockMqttPubSubClient mock = new();
            string execClientId = mock.ClientId;
            int timesCmdExecuted = 0;

            await using EchoWithMetadataCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = $"mock/{execClientId}/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    int executionIndex = Interlocked.Increment(ref timesCmdExecuted);
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request + executionIndex,
                    });
                },
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            string invClientId1 = Guid.NewGuid().ToString();
            string invClientId2 = Guid.NewGuid().ToString();
            string payload = nameof(DuplicateRequest_NotIdempotent_WithinCommandTimeout_DifferentInvokerId_TopicContainsExecutorId_NotRetrievedFromCache);
            var serializer = new Utf8JsonSerializer();
            var correlationData = Guid.NewGuid().ToByteArray();
            var payloadContext = serializer.ToBytes(payload);
            MqttApplicationMessage requestMsg1 = new MqttApplicationMessage($"mock/{execClientId}/echo")
            {
                PayloadSegment = payloadContext.SerializedPayload ?? Array.Empty<byte>(),
                ContentType = payloadContext.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext.PayloadFormatIndicator,
                MessageExpiryInterval = 10,
                ResponseTopic = $"mock/{execClientId}/echo/response",
            };

            requestMsg1.AddUserProperty(AkriSystemProperties.SourceId, invClientId1);

            var payloadContext2 = serializer.ToBytes(payload);
            MqttApplicationMessage requestMsg2 = new MqttApplicationMessage($"mock/{execClientId}/echo")
            {
                PayloadSegment = payloadContext2.SerializedPayload ?? Array.Empty<byte>(),
                ContentType = payloadContext2.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext2.PayloadFormatIndicator,
                MessageExpiryInterval = 10,
                ResponseTopic = $"mock/{execClientId}/echo/response",
            };

            requestMsg2.AddUserProperty(AkriSystemProperties.SourceId, invClientId2);

            await mock.SimulateNewMessage(requestMsg1);
            await mock.SimulateNewMessage(requestMsg2);
            await mock.SimulatedMessageAcknowledged();
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(2, timesCmdExecuted);
            Assert.Equal(2, mock.AcknowledgedMessageCount);
            Assert.Equal(2, mock.MessagesPublished.Count);
            Assert.Equal($"mock/{execClientId}/echo/response", mock.MessagesPublished[0].Topic);
            Assert.Equal($"mock/{execClientId}/echo/response", mock.MessagesPublished[1].Topic);

            // Response messages could arrive in either order
            byte[]? payload1 = serializer.ToBytes(payload + payload + 1).SerializedPayload;
            byte[]? payload2 = serializer.ToBytes(payload + payload + 2).SerializedPayload;
            Assert.True(
                (payload1!.SequenceEqual(mock.MessagesPublished[0].PayloadSegment.Array!) && payload2!.SequenceEqual(mock.MessagesPublished[1].PayloadSegment.Array!)) ||
                (payload1!.SequenceEqual(mock.MessagesPublished[1].PayloadSegment.Array!) && payload2!.SequenceEqual(mock.MessagesPublished[0].PayloadSegment.Array!)));
        }

        [Fact(Skip = "Flaky")]
        public async Task DuplicateRequest_NotIdempotent_WithinCommandTimeout_DifferentInvokerId_TopicWithoutExecutorId_NotRetrievedFromCache()
        {
            MockMqttPubSubClient mock = new();
            int timesCmdExecuted = 0;

            await using EchoWithMetadataCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/any/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    int executionIndex = Interlocked.Increment(ref timesCmdExecuted);
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request + executionIndex,
                    });
                },
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            string invClientId1 = Guid.NewGuid().ToString();
            string invClientId2 = Guid.NewGuid().ToString();
            string payload = nameof(DuplicateRequest_NotIdempotent_WithinCommandTimeout_DifferentInvokerId_TopicWithoutExecutorId_NotRetrievedFromCache);
            var serializer = new Utf8JsonSerializer();
            var correlationData = Guid.NewGuid().ToByteArray();
            var payloadContext = serializer.ToBytes(payload);
            MqttApplicationMessage requestMsg1 = new MqttApplicationMessage("mock/any/echo")
            {
                PayloadSegment = payloadContext.SerializedPayload ?? Array.Empty<byte>(),
                ContentType = payloadContext.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext.PayloadFormatIndicator,
                MessageExpiryInterval = 10,
                ResponseTopic = "mock/any/echo/response",
            };

            requestMsg1.AddUserProperty(AkriSystemProperties.SourceId, invClientId1);

            var payloadContext2 = serializer.ToBytes(payload);
            MqttApplicationMessage requestMsg2 = new MqttApplicationMessage("mock/any/echo")
            {
                PayloadSegment = payloadContext2.SerializedPayload ?? Array.Empty<byte>(),
                ContentType = payloadContext2.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext2.PayloadFormatIndicator,
                MessageExpiryInterval = 10,
                ResponseTopic = "mock/any/echo/response",
            };

            requestMsg2.AddUserProperty(AkriSystemProperties.SourceId, invClientId1);

            await mock.SimulateNewMessage(requestMsg1);
            await mock.SimulateNewMessage(requestMsg2);
            await mock.SimulatedMessageAcknowledged();
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(2, timesCmdExecuted);
            Assert.Equal(2, mock.AcknowledgedMessageCount);
            Assert.Equal(2, mock.MessagesPublished.Count);
            Assert.Equal($"mock/any/echo/response", mock.MessagesPublished[0].Topic);
            Assert.Equal($"mock/any/echo/response", mock.MessagesPublished[1].Topic);

            // Response messages could arrive in either order
            byte[]? payload1 = serializer.ToBytes(payload + payload + 1).SerializedPayload;
            byte[]? payload2 = serializer.ToBytes(payload + payload + 2).SerializedPayload;
            Assert.True(
                (payload1!.SequenceEqual(mock.MessagesPublished[0].PayloadSegment.Array!) && payload2!.SequenceEqual(mock.MessagesPublished[1].PayloadSegment.Array!)) ||
                (payload1!.SequenceEqual(mock.MessagesPublished[1].PayloadSegment.Array!) && payload2!.SequenceEqual(mock.MessagesPublished[0].PayloadSegment.Array!)));
        }

        [Fact]
        public async Task DuplicateRequest_Idempotent_CacheUnexpired_RetrievedFromCache()
        {
            MockMqttPubSubClient mock = new();
            int timesCmdExecuted = 0;

            await using EchoWithMetadataCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    int executionIndex = Interlocked.Increment(ref timesCmdExecuted);
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request + executionIndex,
                    });
                },
                IsIdempotent = true,
                CacheTtl = TimeSpan.FromSeconds(30),
            };
            await echoCommand.StartAsync();

            string payload = nameof(DuplicateRequest_Idempotent_CacheUnexpired_RetrievedFromCache);
            var serializer = new Utf8JsonSerializer();
            var payloadContext = serializer.ToBytes(payload);
            MqttApplicationMessage requestMsg = new MqttApplicationMessage("mock/echo")
            {
                PayloadSegment = payloadContext.SerializedPayload ?? Array.Empty<byte>(),
                ContentType = payloadContext.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext.PayloadFormatIndicator,
                ResponseTopic = "mock/echo/response",
                MessageExpiryInterval = 25,
            };

            requestMsg.AddUserProperty(AkriSystemProperties.SourceId, Guid.NewGuid().ToString());
            requestMsg.AddUserProperty("_failFirstPubAck", "true");

            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.NotNull(mock.MessagePublished);
            Assert.Equal("mock/echo/response", mock.MessagePublished.Topic);
            Assert.Equal(serializer.ToBytes(payload + payload + 1).SerializedPayload, mock.MessagePublished.PayloadSegment.Array);
        }

        [Fact]
        public async Task MaximumConcurrencyOne_ProcessMessagesSequentially()
        {
            SemaphoreSlim semaphore = new(1);
            int currentParallelism = 0;
            int maxObservedParallelism = 0;

            MockMqttPubSubClient mock = new();

            await using DelayCommandExecutor delay = new(mock)
            {
                // There are separate increment and decrement operations for the currentParallelism counter that happen within a semaphore.
                // These increment and decrement operations are separated by a delay.
                // In case of parallel execution, we will see all increments happen first, and then the decrements.
                // In case of sequential execution, we will see an increment, followed by a decrement, followed by another increment, etc.

                RequestTopicPattern = "mock/delay",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    await semaphore.WaitAsync();
                    currentParallelism++;
                    if (currentParallelism > maxObservedParallelism)
                    {
                        maxObservedParallelism = currentParallelism;
                    }
                    semaphore.Release();

                    Debug.Assert(reqMd.Request != null);
                    await Task.Delay(reqMd.Request.TimeSpan);

                    await semaphore.WaitAsync();
                    currentParallelism--;
                    semaphore.Release();

                    return new ExtendedResponse<IntegerClass>()
                    {
                        Response = new IntegerClass { Integer = 200 }
                    };
                },
                IsIdempotent = false,
            };
            await delay.StartAsync(preferredDispatchConcurrency: 1);

            var unlockWait = new TimeSpanClass { TimeSpan = TimeSpan.FromSeconds(2) };
            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = "mock/delay";
            var responseTopic = "mock/delay/response";

            var payloadContext = payloadSerializer.ToBytes(unlockWait);
            MqttApplicationMessage message1 = new MqttApplicationMessage(requestTopic)
            {
                PayloadSegment = payloadContext.SerializedPayload ?? Array.Empty<byte>(),
                ContentType = payloadContext.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext.PayloadFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message1.AddUserProperty(AkriSystemProperties.SourceId, Guid.NewGuid().ToString());

            var payloadContext2 = payloadSerializer.ToBytes(unlockWait);
            MqttApplicationMessage message2 = new MqttApplicationMessage(requestTopic)
            {
                PayloadSegment = payloadContext2.SerializedPayload ?? Array.Empty<byte>(),
                ContentType = payloadContext2.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext2.PayloadFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message2.AddUserProperty(AkriSystemProperties.SourceId, Guid.NewGuid().ToString());

            var payloadContext3 = payloadSerializer.ToBytes(unlockWait);
            MqttApplicationMessage message3 = new MqttApplicationMessage(requestTopic)
            {
                PayloadSegment = payloadContext3.SerializedPayload ?? Array.Empty<byte>(),
                ContentType = payloadContext3.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext3.PayloadFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message3.AddUserProperty(AkriSystemProperties.SourceId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message1);
            await mock.SimulateNewMessage(message2);
            await mock.SimulateNewMessage(message3);

            await mock.SimulatedMessageAcknowledged();
            await mock.SimulatedMessageAcknowledged();
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, maxObservedParallelism);
        }

        [Fact]
        public async Task ExecutorRequestUnexpiredExecutorTimeout_RpcErrorTimeout()
        {
            MockMqttPubSubClient mock = new();
            int timesCmdExecuted = 0;
            TimeSpan timeout = TimeSpan.FromSeconds(10);

            await using EchoWithMetadataCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    Interlocked.Increment(ref timesCmdExecuted);
                    await Task.Delay(2 * timeout, ct);

                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request,
                    });
                },
                IsIdempotent = false,
                ExecutionTimeout = TimeSpan.FromSeconds(1),
            };
            await echoCommand.StartAsync();

            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = $"mock/echo";
            var responseTopic = "mock/echo/response";
            Guid cid = Guid.NewGuid();
            var payloadContext = payloadSerializer.ToBytes(nameof(ExecutorRequestUnexpiredExecutorTimeout_RpcErrorTimeout));
            var message = new MqttApplicationMessage(requestTopic)
            {
                CorrelationData = cid.ToByteArray(),
                PayloadSegment = payloadContext.SerializedPayload ?? Array.Empty<byte>(),
                ContentType = payloadContext.ContentType,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext.PayloadFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = (uint)timeout.TotalSeconds,
            };

            message.AddUserProperty(AkriSystemProperties.SourceId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.NotNull(mock.MessagePublished.UserProperties);
            Assert.True(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.Status, out string? cmdStatus));
            Assert.Equal(((int)CommandStatusCode.RequestTimeout).ToString(CultureInfo.InvariantCulture), cmdStatus);

            Assert.False(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.IsApplicationError, out string? isAppError) && isAppError?.ToLower() == "true");

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.NotNull(mock.MessagePublished);
            Assert.Null(mock.MessagePublished.PayloadSegment.Array);
            Assert.Equal(cid.ToByteArray(), mock.MessagePublished.CorrelationData);
        }

        [Fact]
        public async Task ExecutorResponsePubAckDropped_NoExceptionThrownRequestAcknowledged()
        {
            MockMqttPubSubClient mock = new();
            int timesCmdExecuted = 0;

            await using EchoWithMetadataCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    Interlocked.Increment(ref timesCmdExecuted);
                    CommandResponseMetadata responseMetadata = new();
                    responseMetadata.UserData["_dropPubAck"] = "true";
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request,
                        ResponseMetadata = responseMetadata,
                    });
                },
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = $"mock/echo";
            var responseTopic = "mock/echo/response";
            string payload = nameof(ExecutorResponsePubAckDropped_NoExceptionThrownRequestAcknowledged);
            Guid cid = Guid.NewGuid();
            var payloadContext = payloadSerializer.ToBytes(payload);
            var message = new MqttApplicationMessage(requestTopic)
            {
                CorrelationData = cid.ToByteArray(),
                PayloadSegment = payloadContext.SerializedPayload ?? Array.Empty<byte>(),
                ContentType = payloadContext.ContentType,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext.PayloadFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message.AddUserProperty(AkriSystemProperties.SourceId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.NotNull(mock.MessagePublished);
            Assert.Equal(payloadSerializer.ToBytes(payload + payload).SerializedPayload, mock.MessagePublished.PayloadSegment.Array);
            Assert.Equal(cid.ToByteArray(), mock.MessagePublished.CorrelationData);
        }

        [Fact]
        public async Task CommandExecutor_ThrowsIfAccessedWhenDisposed()
        {
            MockMqttPubSubClient mock = new();
            string topic = "mock/echo/unsubAckUnspecifiedError";
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = topic,
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            echoCommand.TopicTokenMap["modelId"] = "MyModel";

            await echoCommand.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await echoCommand.StartAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await echoCommand.StopAsync());
        }

        [Fact]
        public async Task CommandExecutor_ThrowsIfCancellationRequested()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "irrelevant",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            echoCommand.TopicTokenMap["modelId"] = "MyModel";

            CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await echoCommand.StartAsync(cancellationToken: cts.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await echoCommand.StopAsync(cancellationToken: cts.Token));
        }
    }
}