using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
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
        public void ConstructExecutorWithNullNameThrowsException()
        {
            MockMqttPubSubClient mock = new();
            var exception = Assert.Throws<AkriMqttException>(() =>
            {
                new EchoCommandExecutor(mock, null!)
                {
                    OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
                };
            });
            Assert.Equal(AkriMqttErrorKind.ArgumentInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("commandName", exception.PropertyName);
        }

        [Fact]
        public void ConstructExecutorWithEmptyNameThrowsException()
        {
            MockMqttPubSubClient mock = new();
            var exception = Assert.Throws<AkriMqttException>(() =>
            {
                new EchoCommandExecutor(mock, string.Empty)
                {
                    OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
                };
            });
            Assert.Equal(AkriMqttErrorKind.ArgumentInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("commandName", exception.PropertyName);
            Assert.Equal(string.Empty, exception.PropertyValue);
        }

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
                RequestTopicPattern = "mock/{unknown}/echo",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("RequestTopicPattern", exception.PropertyName);
            Assert.Equal("mock/{unknown}/echo", exception.PropertyValue);
        }

        [Fact]
        public async Task RequestTopicModelIdWithoutReplacementThrowsException()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/{modelId}/echo",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("RequestTopicPattern", exception.PropertyName);
            Assert.Equal("mock/{modelId}/echo", exception.PropertyValue);
        }

        [Fact]
        public async Task RequestTopicModelIdWithInvalidReplacementThrowsException()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/{modelId}/echo",
                ModelId = "Invalid/Model",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("RequestTopicPattern", exception.PropertyName);
            Assert.Equal("mock/{modelId}/echo", exception.PropertyValue);
        }

        [Fact]
        public async Task RequestTopicModelIdWithValidReplacementDoesNotThrow()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/{modelId}/echo",
                ModelId = "MyModel",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };
            await echoCommand.StartAsync();
        }

        [Fact]
        public async Task RequestTopicCommandNameWithInvalidReplacementThrowsException()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock, "invalid/name")
            {
                RequestTopicPattern = "mock/{commandName}/echo",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("RequestTopicPattern", exception.PropertyName);
            Assert.Equal("mock/{commandName}/echo", exception.PropertyValue);
        }

        [Fact]
        public async Task RequestTopicCommandNameWithValidReplacementDoesNotThrow()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/{commandName}/echo",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };
            await echoCommand.StartAsync();
        }

        [Fact]
        public void InvalidTopicNamespaceThrowsException()
        {
            MockMqttPubSubClient mock = new();
            var exception = Assert.Throws<AkriMqttException>(
                () => new EchoCommandExecutor(mock)
                {
                    RequestTopicPattern = "mock/echo",
                    OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
                    TopicNamespace = "invalid/{modelId}",
                });

            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("TopicNamespace", exception.PropertyName);
            Assert.Equal("invalid/{modelId}", exception.PropertyValue);
        }

        [Fact]
        public async Task NonIdempotentCommandNegativeCacheableDurationThrowsException()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                IsIdempotent = false,
                CacheableDuration = TimeSpan.FromSeconds(-1),
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("CacheableDuration", exception.PropertyName);
            Assert.Equal(TimeSpan.FromSeconds(-1), exception.PropertyValue);
        }

        [Fact]
        public async Task IdempotentCommandNegativeCacheableDurationThrowsException()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                IsIdempotent = true,
                CacheableDuration = TimeSpan.FromSeconds(-1),
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("CacheableDuration", exception.PropertyName);
            Assert.Equal(TimeSpan.FromSeconds(-1), exception.PropertyValue);
        }

        [Fact]
        public async Task NonIdempotentCommandZeroCacheableDurationDoesNotThrow()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                IsIdempotent = false,
                CacheableDuration = TimeSpan.Zero,
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };
            await echoCommand.StartAsync();
        }

        [Fact]
        public async Task IdempotentCommandZeroCacheableDurationDoesNotThrow()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                IsIdempotent = true,
                CacheableDuration = TimeSpan.Zero,
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };
            await echoCommand.StartAsync();
        }

        [Fact]
        public async Task NonIdempotentCommandPositiveCacheableDurationThrowsException()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                IsIdempotent = false,
                CacheableDuration = TimeSpan.FromSeconds(1),
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("CacheableDuration", exception.PropertyName);
            Assert.Equal(TimeSpan.FromSeconds(1), exception.PropertyValue);
        }

        [Fact]
        public async Task IdempotentCommandPositiveCacheableDurationDoesNotThrow()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                IsIdempotent = true,
                CacheableDuration = TimeSpan.FromSeconds(1),
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };
            await echoCommand.StartAsync();
        }

        [Fact]
        public async Task ExecutorZeroTimeout_ThrowsException()
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
                ExecutionTimeout = TimeSpan.Zero,
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("ExecutionTimeout", exception.PropertyName);
            Assert.Equal(TimeSpan.Zero, exception.PropertyValue);
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
        public async Task ExecuteCommandOk_SendsAck()
        {
            MockMqttPubSubClient mock = new();
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request
                    });
                }
            };
            await echoCommand.StartAsync();

            string payload = nameof(ExecuteCommandOk_SendsAck);
            var serializer = new Utf8JsonSerializer();
            MqttApplicationMessage requestMsg = new MqttApplicationMessage("mock/echo")
            {
                PayloadSegment = serializer.ToBytes(payload) ?? Array.Empty<byte>(),
                ContentType = serializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                ResponseTopic = "mock/echo/response",
                MessageExpiryInterval = 10,
            };

            requestMsg.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal("mock/echo", mock.SubscribedTopicReceived);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.Equal("mock/echo/response", mock.MessagePublished.Topic);
            Assert.Equal(serializer.ToBytes(payload + payload), mock.MessagePublished.PayloadSegment.Array);
        }

        [Fact]
        public async Task ExecuteCommandWithMetadataOk_SendsAck()
        {
            MockMqttPubSubClient mock = new();
            await using EchoWithMetadataCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    CommandResponseMetadata responseMetadata = new();
                    responseMetadata.UserData["userHeader"] = "userValue";
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request,
                        ResponseMetadata = responseMetadata,
                    });
                }
            };
            await echoCommand.StartAsync();

            string payload = nameof(ExecuteCommandWithMetadataOk_SendsAck);
            var serializer = new Utf8JsonSerializer();
            MqttApplicationMessage requestMsg = new MqttApplicationMessage("mock/echo")
            {
                PayloadSegment = serializer.ToBytes(payload) ?? Array.Empty<byte>(),
                ContentType = serializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                ResponseTopic = "mock/echo/response",
                MessageExpiryInterval = 10,
            };

            requestMsg.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal("mock/echo", mock.SubscribedTopicReceived);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.Equal("mock/echo/response", mock.MessagePublished.Topic);
            Assert.Equal(serializer.ToBytes(payload + payload), mock.MessagePublished.PayloadSegment.Array);

            Assert.NotNull(mock.MessagePublished.UserProperties);
            Assert.True(mock.MessagePublished.UserProperties.TryGetProperty("userHeader", out string? userProp));
            Assert.Equal("userValue", userProp);
        }

        [Fact]
        public async Task DuplicateRequest_NotIdempotent_WithinCommandTimeout_RetrievedFromCache()
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
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            string payload = nameof(DuplicateRequest_NotIdempotent_WithinCommandTimeout_RetrievedFromCache);
            var serializer = new Utf8JsonSerializer();
            MqttApplicationMessage requestMsg = new MqttApplicationMessage("mock/echo")
            {
                PayloadSegment = serializer.ToBytes(payload) ?? Array.Empty<byte>(),
                ContentType = serializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                ResponseTopic = "mock/echo/response",
                MessageExpiryInterval = 10,
            };

            requestMsg.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());
            requestMsg.AddUserProperty("_failFirstPubAck", "true");

            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.Equal("mock/echo/response", mock.MessagePublished.Topic);
            Assert.Equal(serializer.ToBytes(payload + payload + 1), mock.MessagePublished.PayloadSegment.Array);
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

            MqttApplicationMessage requestMsg = new MqttApplicationMessage($"mock/{execClientId}/echo")
            {
                PayloadSegment = serializer.ToBytes(payload) ?? Array.Empty<byte>(),
                ContentType = serializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                MessageExpiryInterval = 10,
                ResponseTopic = $"mock/{execClientId}/echo/response",
            };

            requestMsg.AddUserProperty(AkriSystemProperties.CommandInvokerId, invClientId1);

            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulatedMessageAcknowledged();
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(2, mock.AcknowledgedMessageCount);
            Assert.Equal($"mock/{execClientId}/echo/response", mock.MessagePublished.Topic);
            Assert.Equal(serializer.ToBytes(payload + payload + 1), mock.MessagePublished.PayloadSegment.Array);
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
            MqttApplicationMessage requestMsg = new MqttApplicationMessage($"mock/{execClientId}/echo")
            {
                PayloadSegment = serializer.ToBytes(payload) ?? Array.Empty<byte>(),
                ContentType = serializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                MessageExpiryInterval = 10,
                ResponseTopic = $"mock/{execClientId}/echo/response",
            };

            requestMsg.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulatedMessageAcknowledged();
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(2, mock.AcknowledgedMessageCount);
            Assert.Equal($"mock/{execClientId}/echo/response", mock.MessagePublished.Topic);
            Assert.Equal(serializer.ToBytes(payload + payload + 1), mock.MessagePublished.PayloadSegment.Array);
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
            var CorrelationData = Guid.NewGuid().ToByteArray();
            MqttApplicationMessage requestMsg1 = new MqttApplicationMessage($"mock/{execClientId}/echo")
            {
                PayloadSegment = serializer.ToBytes(payload) ?? Array.Empty<byte>(),
                ContentType = serializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                MessageExpiryInterval = 10,
                ResponseTopic = $"mock/{execClientId}/echo/response",
            };

            requestMsg1.AddUserProperty(AkriSystemProperties.CommandInvokerId, invClientId1);

            MqttApplicationMessage requestMsg2 = new MqttApplicationMessage($"mock/{execClientId}/echo")
            {
                PayloadSegment = serializer.ToBytes(payload) ?? Array.Empty<byte>(),
                ContentType = serializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                MessageExpiryInterval = 10,
                ResponseTopic = $"mock/{execClientId}/echo/response",
            };

            requestMsg2.AddUserProperty(AkriSystemProperties.CommandInvokerId, invClientId2);

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
            byte[]? payload1 = serializer.ToBytes(payload + payload + 1);
            byte[]? payload2 = serializer.ToBytes(payload + payload + 2);
            Assert.True(
                (payload1!.SequenceEqual(mock.MessagesPublished[0].PayloadSegment.Array!) && payload2!.SequenceEqual(mock.MessagesPublished[1].PayloadSegment.Array!)) ||
                (payload1!.SequenceEqual(mock.MessagesPublished[1].PayloadSegment.Array!) && payload2!.SequenceEqual(mock.MessagesPublished[0].PayloadSegment.Array!)));
        }

        [Fact(Skip = "Flacky")]
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
            var CorrelationData = Guid.NewGuid().ToByteArray();
            MqttApplicationMessage requestMsg1 = new MqttApplicationMessage("mock/any/echo")
            {
                PayloadSegment = serializer.ToBytes(payload) ?? Array.Empty<byte>(),
                ContentType = serializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                MessageExpiryInterval = 10,
                ResponseTopic = "mock/any/echo/response",
            };

            requestMsg1.AddUserProperty(AkriSystemProperties.CommandInvokerId, invClientId1);

            MqttApplicationMessage requestMsg2 = new MqttApplicationMessage("mock/any/echo")
            {
                PayloadSegment = serializer.ToBytes(payload) ?? Array.Empty<byte>(),
                ContentType = serializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                MessageExpiryInterval = 10,
                ResponseTopic = "mock/any/echo/response",
            };

            requestMsg2.AddUserProperty(AkriSystemProperties.CommandInvokerId, invClientId1);

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
            byte[]? payload1 = serializer.ToBytes(payload + payload + 1);
            byte[]? payload2 = serializer.ToBytes(payload + payload + 2);
            Assert.True(
                (payload1!.SequenceEqual(mock.MessagesPublished[0].PayloadSegment.Array!) && payload2!.SequenceEqual(mock.MessagesPublished[1].PayloadSegment.Array!)) ||
                (payload1!.SequenceEqual(mock.MessagesPublished[1].PayloadSegment.Array!) && payload2!.SequenceEqual(mock.MessagesPublished[0].PayloadSegment.Array!)));
        }

        [Fact(Skip = "flaky")]
        public async Task EquivalentRequest_NonIdempotent_NotRetrievedFromCache()
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
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            string payload = nameof(EquivalentRequest_NonIdempotent_NotRetrievedFromCache);
            var serializer = new Utf8JsonSerializer();
            Guid cid1 = Guid.NewGuid();
            Guid cid2 = Guid.NewGuid();
            MqttApplicationMessage requestMsg = new MqttApplicationMessage("mock/echo")
            {
                PayloadSegment = serializer.ToBytes(payload) ?? ArraySegment<byte>.Empty,
                ContentType = serializer.ContentType,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                MessageExpiryInterval = (uint)TimeSpan.FromSeconds(5).TotalSeconds,
                ResponseTopic = "mock/echo/response",
                CorrelationData = cid1.ToByteArray()
            };

            requestMsg.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(requestMsg);

            requestMsg.CorrelationData = cid2.ToByteArray();

            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulatedMessageAcknowledged();
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(2, timesCmdExecuted);
            Assert.Equal(2, mock.AcknowledgedMessageCount);
            Assert.Equal(2, mock.MessagesPublished.Count);
            Assert.Equal("mock/echo/response", mock.MessagesPublished[0].Topic);
            Assert.Equal("mock/echo/response", mock.MessagesPublished[1].Topic);

            // Response messages could arrive in either order
            byte[]? payload1 = serializer.ToBytes(payload + payload + 1);
            byte[]? payload2 = serializer.ToBytes(payload + payload + 2);
            Assert.True(
                (payload1!.SequenceEqual(mock.MessagesPublished[0].PayloadSegment.Array!) && payload2!.SequenceEqual(mock.MessagesPublished[1].PayloadSegment.Array!)) ||
                (payload1!.SequenceEqual(mock.MessagesPublished[1].PayloadSegment.Array!) && payload2!.SequenceEqual(mock.MessagesPublished[0].PayloadSegment.Array!)));
        }

        [Fact]
        public async Task DuplicateRequest_Idempotent_WithinCommandTimeout_RetrievedFromCache()
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
                CacheableDuration = TimeSpan.Zero,
            };
            await echoCommand.StartAsync();

            string payload = nameof(DuplicateRequest_Idempotent_WithinCommandTimeout_RetrievedFromCache);
            var serializer = new Utf8JsonSerializer();
            MqttApplicationMessage requestMsg = new MqttApplicationMessage("mock/echo")
            {
                PayloadSegment = serializer.ToBytes(payload) ?? Array.Empty<byte>(),
                ContentType = serializer.ContentType,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                MessageExpiryInterval = (uint)TimeSpan.FromSeconds(10).TotalSeconds,
                ResponseTopic = "mock/echo/response",
            };

            requestMsg.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());
            requestMsg.AddUserProperty("_failFirstPubAck", "true");

            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.Equal("mock/echo/response", mock.MessagePublished.Topic);
            Assert.Equal(serializer.ToBytes(payload + payload + 1), mock.MessagePublished.PayloadSegment.Array);
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
                CacheableDuration = TimeSpan.FromSeconds(30),
            };
            await echoCommand.StartAsync();

            string payload = nameof(DuplicateRequest_Idempotent_CacheUnexpired_RetrievedFromCache);
            var serializer = new Utf8JsonSerializer();
            MqttApplicationMessage requestMsg = new MqttApplicationMessage("mock/echo")
            {
                PayloadSegment = serializer.ToBytes(payload) ?? Array.Empty<byte>(),
                ContentType = serializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                ResponseTopic = "mock/echo/response",
                MessageExpiryInterval = 25,
            };

            requestMsg.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());
            requestMsg.AddUserProperty("_failFirstPubAck", "true");

            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.NotNull(mock.MessagePublished);
            Assert.Equal("mock/echo/response", mock.MessagePublished.Topic);
            Assert.Equal(serializer.ToBytes(payload + payload + 1), mock.MessagePublished.PayloadSegment.Array);
        }

        [Fact]
        public async Task EquivalentRequest_Idempotent_CacheUnexpired_RetrievedFromCache()
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
                CacheableDuration = TimeSpan.FromSeconds(30),
            };
            await echoCommand.StartAsync();

            string payload = nameof(EquivalentRequest_Idempotent_CacheUnexpired_RetrievedFromCache);
            var serializer = new Utf8JsonSerializer();
            Guid cid1 = Guid.NewGuid();
            Guid cid2 = Guid.NewGuid();
            MqttApplicationMessage requestMsg = new MqttApplicationMessage("mock/echo")
            {
                PayloadSegment = serializer.ToBytes(payload) ?? ArraySegment<byte>.Empty,
                ContentType = serializer.ContentType,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                MessageExpiryInterval = (uint)TimeSpan.FromSeconds(25).TotalSeconds,
                ResponseTopic = "mock/echo/response",
                CorrelationData = cid1.ToByteArray()
            };

            requestMsg.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulatedMessageAcknowledged();

            requestMsg.CorrelationData = cid2.ToByteArray();

            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(2, mock.AcknowledgedMessageCount);
            Assert.Equal("mock/echo/response", mock.MessagePublished.Topic);
            Assert.Equal(serializer.ToBytes(payload + payload + 1), mock.MessagePublished.PayloadSegment.Array);
        }

        [Fact(Skip = "flaky")]
        public async Task EquivalentRequest_Idempotent_CacheExpired_NotRetrievedFromCache()
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
                CacheableDuration = TimeSpan.Zero,
            };
            await echoCommand.StartAsync();

            string payload = nameof(EquivalentRequest_Idempotent_CacheExpired_NotRetrievedFromCache);
            var serializer = new Utf8JsonSerializer();
            string cid1 = Guid.NewGuid().ToString();
            string cid2 = Guid.NewGuid().ToString();
            MqttApplicationMessage requestMsg = new MqttApplicationMessage("mock/echo")
            {
                PayloadSegment = serializer.ToBytes(payload) ?? Array.Empty<byte>(),
                ContentType = serializer.ContentType,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                ResponseTopic = "mock/echo/response",
                MessageExpiryInterval = 25,
            };

            requestMsg.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            requestMsg.CorrelationData = Encoding.UTF8.GetBytes(cid1);
            await mock.SimulateNewMessage(requestMsg);
            requestMsg.CorrelationData = Encoding.UTF8.GetBytes(cid2);
            await mock.SimulateNewMessage(requestMsg);
            await mock.SimulatedMessageAcknowledged();
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(2, timesCmdExecuted);
            Assert.Equal(2, mock.AcknowledgedMessageCount);
            Assert.Equal(2, mock.MessagesPublished.Count);
            Assert.Equal("mock/echo/response", mock.MessagesPublished[0].Topic);
            Assert.Equal("mock/echo/response", mock.MessagesPublished[1].Topic);

            // Response messages could arrive in either order
            byte[]? payload1 = serializer.ToBytes(payload + payload + 1);
            byte[]? payload2 = serializer.ToBytes(payload + payload + 2);
            Assert.True(
                (payload1!.SequenceEqual(mock.MessagesPublished[0].PayloadSegment.Array!) && payload2!.SequenceEqual(mock.MessagesPublished[1].PayloadSegment.Array!)) ||
                (payload1!.SequenceEqual(mock.MessagesPublished[1].PayloadSegment.Array!) && payload2!.SequenceEqual(mock.MessagesPublished[0].PayloadSegment.Array!)));
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

            MqttApplicationMessage message1 = new MqttApplicationMessage(requestTopic)
            {
                PayloadSegment = payloadSerializer.ToBytes(unlockWait) ?? Array.Empty<byte>(),
                ContentType = payloadSerializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message1.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            MqttApplicationMessage message2 = new MqttApplicationMessage(requestTopic)
            {
                PayloadSegment = payloadSerializer.ToBytes(unlockWait) ?? Array.Empty<byte>(),
                ContentType = payloadSerializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message2.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            MqttApplicationMessage message3 = new MqttApplicationMessage(requestTopic)
            {
                PayloadSegment = payloadSerializer.ToBytes(unlockWait) ?? Array.Empty<byte>(),
                ContentType = payloadSerializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message3.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message1);
            await mock.SimulateNewMessage(message2);
            await mock.SimulateNewMessage(message3);

            await mock.SimulatedMessageAcknowledged();
            await mock.SimulatedMessageAcknowledged();
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, maxObservedParallelism);
        }

        [Fact]
        public async Task MaximumConcurrencyGreaterThanOne_ProcessMessagesInParallel()
        {
            SemaphoreSlim semaphore = new(1);
            int currentParallelism = 0;
            int maxObservedParallelism = 0;

            MockMqttPubSubClient mock = new();

            await using DelayCommandExecutor delay = new(mock)
            {
                RequestTopicPattern = "mock/delay",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    // There are separate increment and decrement operations for the currentParallelism counter that happen within a semaphore.
                    // These increment and decrement operations are separated by a delay.
                    // In case of parallel execution, we will see all increments happen first, and then the decrements.
                    // In case of sequential execution, we will see an increment, followed by a decrement, followed by another increment, etc.

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
            await delay.StartAsync(preferredDispatchConcurrency: 10);

            var unlockWait = new TimeSpanClass { TimeSpan = TimeSpan.FromSeconds(2) };
            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = "mock/delay";
            var responseTopic = "mock/delay/response";
            MqttApplicationMessage message1 = new MqttApplicationMessage(requestTopic)
            {
                PayloadSegment = payloadSerializer.ToBytes(unlockWait) ?? Array.Empty<byte>(),
                ContentType = payloadSerializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message1.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            MqttApplicationMessage message2 = new MqttApplicationMessage(requestTopic)
            {
                PayloadSegment = payloadSerializer.ToBytes(unlockWait) ?? Array.Empty<byte>(),
                ContentType = payloadSerializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message2.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            MqttApplicationMessage message3 = new MqttApplicationMessage(requestTopic)
            {
                PayloadSegment = payloadSerializer.ToBytes(unlockWait) ?? Array.Empty<byte>(),
                ContentType = payloadSerializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message3.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message1);
            await mock.SimulateNewMessage(message2);
            await mock.SimulateNewMessage(message3);

            await mock.SimulatedMessageAcknowledged();
            await mock.SimulatedMessageAcknowledged();
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(3, maxObservedParallelism);
        }

        [Fact]
        public async Task RequestTopicMismatch_MessageDiscarded()
        {
            MockMqttPubSubClient mock = new();
            int timesCmdExecuted = 0;

            await using EchoWithMetadataCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    Interlocked.Increment(ref timesCmdExecuted);
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request,
                    });
                },
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = $"mock/extraEcho";
            var responseTopic = "mock/extraEcho/response";
            var message = new MqttApplicationMessage(requestTopic)
            {
                PayloadSegment = payloadSerializer.ToBytes(nameof(RequestTopicMismatch_MessageDiscarded)) ?? Array.Empty<byte>(),
                ContentType = payloadSerializer.ContentType,
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(0, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount); // RPC isn't ack-ing this, the mock has autoacknowledge turned on
            Assert.Null(mock.MessagePublished);
        }

        [Fact]
        public async Task CorrelationDataMissing_RpcErrorBadRequest()
        {
            MockMqttPubSubClient mock = new();
            int timesCmdExecuted = 0;

            await using EchoWithMetadataCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    Interlocked.Increment(ref timesCmdExecuted);
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request,
                    });
                },
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = $"mock/echo";
            var responseTopic = "mock/echo/response";
            var message = new MqttApplicationMessage(requestTopic)
            {
                PayloadSegment = payloadSerializer.ToBytes(nameof(CorrelationDataMissing_RpcErrorBadRequest)) ?? ArraySegment<byte>.Empty,
                ResponseTopic = responseTopic,
                ContentType = payloadSerializer.ContentType,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                MessageExpiryInterval = (uint)TimeSpan.FromSeconds(10).TotalSeconds,
            };

            message.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(0, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.NotNull(mock.MessagePublished);
            Assert.Null(mock.MessagePublished.PayloadSegment.Array);

            Assert.True(mock.MessagePublished.UserProperties!.TryGetProperty(AkriSystemProperties.Status, out string? cmdStatus));
            Assert.Equal(((int)CommandStatusCode.BadRequest).ToString(CultureInfo.InvariantCulture), cmdStatus);

            Assert.False(mock.MessagePublished.UserProperties!.TryGetProperty(AkriSystemProperties.IsApplicationError, out string? isAppError) && isAppError?.ToLower() == "true");
        }

        [Fact]
        public async Task ResponseTopicMissing_MessageNotProcessedButAcknowledged()
        {
            MockMqttPubSubClient mock = new();
            int timesCmdExecuted = 0;

            await using EchoWithMetadataCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    Interlocked.Increment(ref timesCmdExecuted);
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request,
                    });
                },
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = $"mock/echo";
            var message = new MqttApplicationMessage(requestTopic)
            {
                CorrelationData = Guid.NewGuid().ToByteArray(),
                PayloadSegment = payloadSerializer.ToBytes(nameof(ResponseTopicMissing_MessageNotProcessedButAcknowledged)) ?? Array.Empty<byte>(),
                ContentType = payloadSerializer.ContentType,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                MessageExpiryInterval = 10,

            };

            message.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(0, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.Null(mock.MessagePublished);
        }

        [Fact]
        public async Task ContentTypeMissing_AckOK()
        {
            MockMqttPubSubClient mock = new();
            int timesCmdExecuted = 0;

            await using EchoWithMetadataCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    Interlocked.Increment(ref timesCmdExecuted);
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request,
                    });
                },
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = $"mock/echo";
            var responseTopic = "mock/echo/response";
            Guid cid = Guid.NewGuid();
            var message = new MqttApplicationMessage(requestTopic)
            {
                CorrelationData = cid.ToByteArray(),
                PayloadSegment = payloadSerializer.ToBytes(nameof(ContentTypeMissing_AckOK)) ?? Array.Empty<byte>(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.NotNull(mock.MessagePublished);
            Assert.NotNull(mock.MessagePublished.PayloadSegment.Array);
            Assert.Equal(cid.ToByteArray(), mock.MessagePublished.CorrelationData);

            Assert.NotNull(mock.MessagePublished.UserProperties);
            Assert.True(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.Status, out string? cmdStatus));
            Assert.Equal(((int)CommandStatusCode.OK).ToString(CultureInfo.InvariantCulture), cmdStatus);
        }

        [Fact]
        public async Task ContentTypeMismatch_RpcErrorUnsupportedMediaType()
        {
            MockMqttPubSubClient mock = new();
            int timesCmdExecuted = 0;

            await using EchoWithMetadataCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    Interlocked.Increment(ref timesCmdExecuted);
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request,
                    });
                },
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = $"mock/echo";
            var responseTopic = "mock/echo/response";
            Guid cid = Guid.NewGuid();
            var message = new MqttApplicationMessage(requestTopic)
            {
                CorrelationData = cid.ToByteArray(),
                PayloadSegment = payloadSerializer.ToBytes(nameof(ContentTypeMismatch_RpcErrorUnsupportedMediaType)) ?? Array.Empty<byte>(),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.Unspecified,
                MessageExpiryInterval = 10,
                ResponseTopic = responseTopic,
                ContentType = "raw/0",
            };

            message.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(0, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.NotNull(mock.MessagePublished);
            Assert.Null(mock.MessagePublished.PayloadSegment.Array);
            Assert.Equal(cid.ToByteArray(), mock.MessagePublished.CorrelationData);

            Assert.NotNull(mock.MessagePublished.UserProperties);
            Assert.True(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.Status, out string? cmdStatus));
            Assert.Equal(((int)CommandStatusCode.UnsupportedMediaType).ToString(CultureInfo.InvariantCulture), cmdStatus);

            Assert.False(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.IsApplicationError, out string? isAppError) && isAppError?.ToLower() == "true");
        }

        [Fact]
        public async Task MissingRequestPayload_RpcErrorBadRequest()
        {
            MockMqttPubSubClient mock = new();
            int timesCmdExecuted = 0;

            await using EchoWithMetadataCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = async (reqMd, ct) =>
                {
                    Interlocked.Increment(ref timesCmdExecuted);
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request,
                    });
                },
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = $"mock/echo";
            var responseTopic = "mock/echo/response";
            Guid cid = Guid.NewGuid();
            var message = new MqttApplicationMessage(requestTopic)
            {
                CorrelationData = cid.ToByteArray(),
                ContentType = payloadSerializer.ContentType,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(0, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.NotNull(mock.MessagePublished);
            Assert.Null(mock.MessagePublished.PayloadSegment.Array);
            Assert.Equal(cid.ToByteArray(), mock.MessagePublished.CorrelationData);

            Assert.NotNull(mock.MessagePublished.UserProperties);
            Assert.True(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.Status, out string? cmdStatus));
            Assert.Equal(((int)CommandStatusCode.BadRequest).ToString(CultureInfo.InvariantCulture), cmdStatus);

            Assert.False(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.IsApplicationError, out string? isAppError) && isAppError?.ToLower() == "true");
        }

        [Fact]
        public async Task DeserializationFailure_RpcErrorBadRequest()
        {
            MockMqttPubSubClient mock = new();
            int timesCmdExecuted = 0;

            await using DelayCommandExecutor delayCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = (reqMd, ct) =>
                {
                    Interlocked.Increment(ref timesCmdExecuted);
                    Debug.Assert(reqMd.Request != null);

                    return Task.FromResult(new ExtendedResponse<IntegerClass>()
                    {
                        Response = new IntegerClass { Integer = 200 }
                    });
                },
                IsIdempotent = false,
            };
            await delayCommand.StartAsync();

            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = $"mock/echo";
            var responseTopic = "mock/echo/response";
            Guid cid = Guid.NewGuid();
            var message = new MqttApplicationMessage(requestTopic)
            {
                CorrelationData = cid.ToByteArray(),
                PayloadSegment = payloadSerializer.ToBytes("not an int") ?? Array.Empty<byte>(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(0, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.NotNull(mock.MessagePublished);
            Assert.Null(mock.MessagePublished.PayloadSegment.Array);
            Assert.Equal(cid.ToByteArray(), mock.MessagePublished.CorrelationData);

            Assert.NotNull(mock.MessagePublished.UserProperties);
            Assert.True(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.Status, out string? cmdStatus));
            Assert.Equal(((int)CommandStatusCode.BadRequest).ToString(CultureInfo.InvariantCulture), cmdStatus);

            Assert.False(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.IsApplicationError, out string? isAppError) && isAppError?.ToLower() == "true");
        }

        [Fact]
        public async Task ExecuteCommandWithException_ApplicationErrorNotImplementedException()
        {
            MockMqttPubSubClient mock = new();
            int timesCmdExecuted = 0;

            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = (reqMd, ct) =>
                {
                    Interlocked.Increment(ref timesCmdExecuted);
                    throw new NotImplementedException();
                },
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = "mock/echo";
            var responseTopic = "mock/echo/response";
            Guid cid = Guid.NewGuid();
            var message = new MqttApplicationMessage(requestTopic)
            {
                CorrelationData = cid.ToByteArray(),
                PayloadSegment = payloadSerializer.ToBytes(nameof(ExecuteCommandWithException_ApplicationErrorNotImplementedException)) ?? Array.Empty<byte>(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
                ContentType = payloadSerializer.ContentType,
            };

            message.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.NotNull(mock.MessagePublished);
            Assert.Null(mock.MessagePublished.PayloadSegment.Array);
            Assert.Equal(cid.ToByteArray(), mock.MessagePublished.CorrelationData);

            Assert.NotNull(mock.MessagePublished.UserProperties);
            Assert.True(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.Status, out string? cmdStatus));
            Assert.Equal(((int)CommandStatusCode.InternalServerError).ToString(CultureInfo.InvariantCulture), cmdStatus);

            Assert.True(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.IsApplicationError, out string? isAppError));
            Assert.Equal("true", isAppError?.ToLower());
        }

        [Fact]
        public async Task ExecuteCommandThatSetsInvalidResponseMetadata_ApplicationErrorInternalServerError()
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
                    responseMetadata.UserData["__hasReservePrefix"] = "userValue";
                    return await Task.FromResult(new ExtendedResponse<string>()
                    {
                        Response = reqMd.Request + reqMd.Request,
                        ResponseMetadata = responseMetadata,
                    });
                }
            };
            await echoCommand.StartAsync();

            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = "mock/echo";
            var responseTopic = "mock/echo/response";
            Guid cid = Guid.NewGuid();
            var message = new MqttApplicationMessage(requestTopic)
            {
                CorrelationData = cid.ToByteArray(),
                PayloadSegment = payloadSerializer.ToBytes(nameof(ExecuteCommandThatSetsInvalidResponseMetadata_ApplicationErrorInternalServerError)) ?? Array.Empty<byte>(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.NotNull(mock.MessagePublished);
            Assert.Equal(cid.ToByteArray(), mock.MessagePublished.CorrelationData);

            Assert.NotNull(mock.MessagePublished.UserProperties);
            Assert.True(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.Status, out string? cmdStatus));
            Assert.Equal(((int)CommandStatusCode.InternalServerError).ToString(CultureInfo.InvariantCulture), cmdStatus);

            Assert.True(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.IsApplicationError, out string? isAppError));
            Assert.Equal("true", isAppError?.ToLower());
        }

        [Fact]
        public async Task ExecuteCommandWithException_ApplicationErrorInvocationException()
        {
            MockMqttPubSubClient mock = new();
            int timesCmdExecuted = 0;

            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = "mock/echo",
                OnCommandReceived = (reqMd, ct) =>
                {
                    Interlocked.Increment(ref timesCmdExecuted);
                    throw new InvocationException();
                },
                IsIdempotent = false,
            };
            await echoCommand.StartAsync();

            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = $"mock/echo";
            var responseTopic = "mock/echo/response";
            Guid cid = Guid.NewGuid();
            var message = new MqttApplicationMessage(requestTopic)
            {
                CorrelationData = cid.ToByteArray(),
                PayloadSegment = payloadSerializer.ToBytes(nameof(ExecuteCommandWithException_ApplicationErrorInvocationException)) ?? Array.Empty<byte>(),
                ContentType = payloadSerializer.ContentType,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.NotNull(mock.MessagePublished);
            Assert.Null(mock.MessagePublished.PayloadSegment.Array);
            Assert.Equal(cid.ToByteArray(), mock.MessagePublished.CorrelationData);

            Assert.NotNull(mock.MessagePublished.UserProperties);
            Assert.True(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.Status, out string? cmdStatus));
            Assert.Equal(((int)HttpStatusCode.UnprocessableContent).ToString(CultureInfo.InvariantCulture), cmdStatus);

            Assert.True(mock.MessagePublished.UserProperties.TryGetProperty(AkriSystemProperties.IsApplicationError, out string? isAppError));
            Assert.Equal("true", isAppError?.ToLower());
        }

        [Fact(Skip = "Flaky test, time dependent, will be replaced by new declarative unit test")]
        public async Task ExecutorRequestExpiresDuringProcessing_NoResponseSentAcknowledgedOk()
        {
            MockMqttPubSubClient mock = new();
            int timesCmdExecuted = 0;
            TimeSpan timeout = TimeSpan.FromSeconds(2);

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
            };
            await echoCommand.StartAsync();

            var payloadSerializer = new Utf8JsonSerializer();
            var requestTopic = $"mock/echo";
            var responseTopic = "mock/echo/response";
            Guid cid = Guid.NewGuid();
            var message = new MqttApplicationMessage(requestTopic)
            {
                CorrelationData = cid.ToByteArray(),
                PayloadSegment = payloadSerializer.ToBytes(nameof(ExecutorRequestExpiresDuringProcessing_NoResponseSentAcknowledgedOk)) ?? Array.Empty<byte>(),
                ContentType = payloadSerializer.ContentType,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = (uint)timeout.TotalSeconds,
            };

            message.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.Null(mock.MessagePublished);
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
            var message = new MqttApplicationMessage(requestTopic)
            {
                CorrelationData = cid.ToByteArray(),
                PayloadSegment = payloadSerializer.ToBytes(nameof(ExecutorRequestUnexpiredExecutorTimeout_RpcErrorTimeout)) ?? Array.Empty<byte>(),
                ContentType = payloadSerializer.ContentType,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = (uint)timeout.TotalSeconds,
            };

            message.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

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
        public async Task ExecutorResponsePubAckFailure_NoExceptionThrownRequestAcknowledged()
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
                    responseMetadata.UserData["_failPubAck"] = "true";
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
            string payload = nameof(ExecutorResponsePubAckFailure_NoExceptionThrownRequestAcknowledged);
            Guid cid = Guid.NewGuid();
            var message = new MqttApplicationMessage(requestTopic)
            {
                CorrelationData = cid.ToByteArray(),
                PayloadSegment = payloadSerializer.ToBytes(payload) ?? Array.Empty<byte>(),
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.NotNull(mock.MessagePublished);
            Assert.Equal(payloadSerializer.ToBytes(payload + payload), mock.MessagePublished.PayloadSegment.Array);
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
            var message = new MqttApplicationMessage(requestTopic)
            {
                CorrelationData = cid.ToByteArray(),
                PayloadSegment = payloadSerializer.ToBytes(payload) ?? Array.Empty<byte>(),
                ContentType = payloadSerializer.ContentType,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadSerializer.CharacterDataFormatIndicator,
                ResponseTopic = responseTopic,
                MessageExpiryInterval = 10,
            };

            message.AddUserProperty(AkriSystemProperties.CommandInvokerId, Guid.NewGuid().ToString());

            await mock.SimulateNewMessage(message);
            await mock.SimulatedMessageAcknowledged();

            Assert.Equal(1, timesCmdExecuted);
            Assert.Equal(1, mock.AcknowledgedMessageCount);
            Assert.NotNull(mock.MessagePublished);
            Assert.Equal(payloadSerializer.ToBytes(payload + payload), mock.MessagePublished.PayloadSegment.Array);
            Assert.Equal(cid.ToByteArray(), mock.MessagePublished.CorrelationData);
        }

        [Fact]
        public async Task ExecutorStartAsync_SubAckFailedWithUnspecifiedError()
        {
            MockMqttPubSubClient mock = new();
            string topic = "mock/echo/subAckUnspecifiedError";
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = topic,
                ModelId = "MyModel",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StartAsync());
            Assert.Equal(AkriMqttErrorKind.MqttError, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);

            var expectedExMessage = $"Failed to subscribe to topic '{topic}' because {MqttClientSubscribeReasonCode.UnspecifiedError}.";
            Assert.Equal(expectedExMessage, ex.Message);
        }

        [Fact]
        public async Task ExecutorStartAsync_UnsubAckFailedWithUnspecifiedError()
        {
            MockMqttPubSubClient mock = new();
            string topic = "mock/echo/unsubAckUnspecifiedError";
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = topic,
                ModelId = "MyModel",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            await echoCommand.StartAsync();

            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => echoCommand.StopAsync());
            Assert.Equal(AkriMqttErrorKind.MqttError, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);

            var expectedExMessage = $"Failed to unsubscribe from topic '{topic}' because {MqttClientSubscribeReasonCode.UnspecifiedError}.";
            Assert.Equal(expectedExMessage, ex.Message);
        }

        [Fact]
        public async Task CommandExecutor_ThrowsIfAccessedWhenDisposed()
        {
            MockMqttPubSubClient mock = new();
            string topic = "mock/echo/unsubAckUnspecifiedError";
            await using EchoCommandExecutor echoCommand = new(mock)
            {
                RequestTopicPattern = topic,
                ModelId = "MyModel",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

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
                ModelId = "MyModel",
                OnCommandReceived = (reqMd, ct) => Task.FromResult(new ExtendedResponse<string>()),
            };

            CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await echoCommand.StartAsync(cancellationToken: cts.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await echoCommand.StopAsync(cancellationToken: cts.Token));
        }
    }
}